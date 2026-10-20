using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using EdgeOfPlain.Core.Content;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Resource;
using EdgeOfPlain.Core.Utils;

namespace EdgeOfPlain.Core.Framework.Mods;

public static class ModManager
{
	public record struct LoadingAssetsData(string Path, ModInfo SourceMod);
	public const string TomlContentsDirectory = "Contents";
	private static readonly List<Mod> _registeredMods = [];
	private static readonly Dictionary<Mod, ModInfo> _infos = [];
	private static readonly List<Mod> _loadedMods = [];
	private static readonly ReadOnlyCollection<Mod> _loadedModsView = _loadedMods.AsReadOnly();
	public static bool IsLoaded { get; private set; }
	private static readonly IReadOnlySet<string> _internalRetentionIds = ModFileSystem.ReservedNamespaces;
	private static readonly Dictionary<string, Action<LoadingAssetsData>> _resourceLoaders =
	new()
	 {
		{ TomlContentsDirectory, LoadTomlContentData},
	};

	private static void LoadTomlContentData(LoadingAssetsData data)
	{
		var content = ContentSerializer.Deserialize<ContentDef>(AssetsManager.LoadResource<string>(data.Path)) ?? throw new InvalidDataException("the file does not describe any content");
		content.SourceMod = data.SourceMod;
		ContentStore.Add(content);
	}

	public static IReadOnlyList<Mod> LoadedMods => _loadedModsView;

	public static bool TryRegisterLoaders(string directory, Action<LoadingAssetsData> action)
		=> _resourceLoaders.TryAdd(directory, action);

	public static void RegisterMod(Mod mod, ModInfo info)
	{
		CheckLoaded();
		ArgumentNullException.ThrowIfNull(mod);
		if (string.IsNullOrEmpty(info.Id)) throw new ArgumentException("Mod must have a non-empty id.", nameof(info));
		if (_internalRetentionIds.Contains(info.Id))
			throw new InvalidOperationException($"Mod '{info.Id}' uses an internal reserved id");
		if (_registeredMods.Exists(registered => string.Equals(registered.ModInfo.Id, info.Id, StringComparison.Ordinal)))
			throw new InvalidOperationException($"Mod '{info.Id}' has already been registered");

		_infos.Add(mod, info);
		_registeredMods.Add(mod);
	}

	public static void LoadAllMods()
	{
		CheckLoaded();
		IsLoaded = true;
		LoadAssemblyMods();
		ContentSerializer.ResetOptions();
		LoadOtherResource();
		ContentStore.Seal();
	}

	private static void LoadOtherResource()
	{
		foreach (var (mod, path, action) in from mod in _loadedMods from kvp in _resourceLoaders select (mod, kvp.Key, kvp.Value))
		{
			var directory = $"{Launcher.AssetsHead}://{mod.ModInfo.Id}/{path}/";
			foreach (var file in FileIO.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
			{
				try
				{
					action(new LoadingAssetsData(file, mod.ModInfo));
				}
				catch (Exception e)
				{
					LogUtils.LogMessage(e.Message, LogUtils.LogType.Error);
				}
			}
		}
	}

	internal static void Reset()
	{
		_registeredMods.Clear();
		_infos.Clear();
		_loadedMods.Clear();
		IsLoaded = false;
	}

	private static void LoadAssemblyMods()
	{

		var order = BuildLoadOrder();
		foreach (var current in order)
		{
			var unloadedDependency = FindUnloadedDependency(current);
			if (unloadedDependency is not null)
			{
				LogUtils.LogMessage(
					$"Mod '{current.ModInfo.Id}' will not be loaded: it depends on '{unloadedDependency}', which was not loaded.",
					LogUtils.LogType.Warning);
				continue;
			}

			var context = new ModContext(current);
			var currentRegisterTypes = current.RegisterTypes ?? [];
			var failed = false;
			try
			{
				foreach (var registerType in currentRegisterTypes)
					ContentTypeRegistry.RegisterType(registerType);
				current.InitMod(context);
			}
			catch(Exception e)
			{
				failed = true;
				LogUtils.LogMessage($"Mod '{current.ModInfo.Id}' failed to load: {e.Message}", LogUtils.LogType.Error);
				foreach (var type in currentRegisterTypes)
					ContentTypeRegistry.TryUnRegisterType(type);
				context.Return();
			}
			context.Dispose();
			if (!failed) _loadedMods.Add(current);
		}
	}

	private static string? FindUnloadedDependency(Mod mod)
	{
		var dependence = mod.ModInfo.Dependence;
		return dependence is null
			? null
			: (from required in dependence
				where !string.IsNullOrEmpty(required.Id)
				where !_loadedMods.Exists(loaded => string.Equals(loaded.ModInfo.Id, required.Id, StringComparison.Ordinal))
				select required.Id).FirstOrDefault();

	}

	private static void CheckLoaded()
	{
		if (IsLoaded) throw new InvalidOperationException("Mods are already loaded, the mod manager can not be changed any more.");
	}

	private static ReadOnlyCollection<Mod> BuildLoadOrder()
	{
		var coreMod = ResolveCoreMod();

		var byId = new Dictionary<string, Mod>(_registeredMods.Count + 1, StringComparer.Ordinal);
		foreach (var mod in _registeredMods) byId[mod.ModInfo.Id] = mod;
		byId[coreMod.ModInfo.Id] = coreMod;

		var ordered = new List<Mod>(_registeredMods.Count + 1) { coreMod };
		var placed = new HashSet<string>(StringComparer.Ordinal) { coreMod.ModInfo.Id };
		var skipped = new HashSet<string>(StringComparer.Ordinal);

		var pending = new List<Mod>(_registeredMods.Count);
		foreach (var mod in _registeredMods.Where(mod => !ReferenceEquals(mod, coreMod)))
		{
			if (string.Equals(mod.ModInfo.Id, coreMod.ModInfo.Id, StringComparison.Ordinal))
			{
				LogUtils.LogMessage($"Mod '{mod.ModInfo.Id}' reuses the id of the built-in mod and will not be loaded.",
					LogUtils.LogType.Error);
				continue;
			}

			pending.Add(mod);
		}
		pending.Sort(CompareModIds);

		while (pending.Count > 0)
		{
			var ready = -1;
			for (var i = 0; i < pending.Count; i++)
			{
				if (GetDependencyState(pending[i], placed, skipped, byId, out _) != DependencyState.Satisfied) continue;
				ready = i;
				break;
			}

			if (ready >= 0)
			{
				var mod = pending[ready];
				pending.RemoveAt(ready);
				ordered.Add(mod);
				placed.Add(mod.ModInfo.Id);
				continue;
			}

			var broken = -1;
			string? problem = null;
			for (var i = 0; i < pending.Count; i++)
			{
				if (GetDependencyState(pending[i], placed, skipped, byId, out problem) != DependencyState.Broken) continue;
				broken = i;
				break;
			}

			if (broken < 0)
			{
				LogUtils.LogMessage(
					$"Cyclic mod dependency detected among: {string.Join(", ", pending.ConvertAll(mod => mod.ModInfo.Id))};" +
					$" these mods will not be loaded.",
					LogUtils.LogType.Error);
				foreach (var mod in pending) skipped.Add(mod.ModInfo.Id);
				pending.Clear();
				break;
			}

			var droppedMod = pending[broken];
			LogUtils.LogMessage($"Mod '{droppedMod.ModInfo.Id}' will not be loaded: {problem}.", LogUtils.LogType.Error);
			skipped.Add(droppedMod.ModInfo.Id);
			pending.RemoveAt(broken);
		}

		return ordered.AsReadOnly();
	}

	private static Base ResolveCoreMod()
	{
		foreach (var mod in _registeredMods.OfType<Base>()) return mod;
		return new Base();
	}

	private static DependencyState GetDependencyState(
		Mod mod,
		HashSet<string> placed,
		HashSet<string> skipped,
		Dictionary<string, Mod> byId,
		out string? problem)
	{
		problem = null;
		var dependence = mod.ModInfo.Dependence;
		if (dependence is null) return DependencyState.Satisfied;

		var waiting = false;
		foreach (var required in dependence)
		{
			if (string.IsNullOrEmpty(required.Id))
			{
				problem = "it declares a dependency without an id";
				return DependencyState.Broken;
			}

			if (!byId.TryGetValue(required.Id, out var provider))
			{
				problem = $"it depends on '{required.Id}', which is not registered";
				return DependencyState.Broken;
			}

			if (skipped.Contains(required.Id))
			{
				problem = $"it depends on '{required.Id}', which will not be loaded";
				return DependencyState.Broken;
			}

			if (provider.ModInfo.Version is null || required.Version is null)
			{
				problem = $"it depends on '{required.Id}', but a version is missing on one side";
				return DependencyState.Broken;
			}

			if (provider.ModInfo.Version < required.Version)
			{
				problem = $"it requires '{required.Id}' >= {required.Version}, but {provider.ModInfo.Version} is registered";
				return DependencyState.Broken;
			}

			if (!placed.Contains(required.Id)) waiting = true;
		}

		return waiting ? DependencyState.Waiting : DependencyState.Satisfied;
	}

	internal static void UnregisterMod(Mod mod)
	{
		_infos.Remove(mod);
		_registeredMods.Remove(mod);
	}

	public static ModInfo GetModInfo(Mod mod)
		=> _infos.TryGetValue(mod, out var info)
			? info
			: throw new InvalidOperationException($"Mod '{mod.GetType().FullName}' has not been registered.");

	private static int CompareModIds(Mod left, Mod right)
		=> string.Compare(left.ModInfo.Id, right.ModInfo.Id, StringComparison.Ordinal);

	private enum DependencyState { Satisfied, Waiting, Broken }
}
