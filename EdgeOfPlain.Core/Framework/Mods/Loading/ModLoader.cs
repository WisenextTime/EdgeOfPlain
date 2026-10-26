using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Components;
using EdgeOfPlain.Core.Utils;

namespace EdgeOfPlain.Core.Framework.Mods.Loading;

public static class ModLoader
{
	private static readonly List<ModPackage> _packages = [];
	private static readonly List<(Mod Mod, ModPackage Package)> _discovered = [];
	private static readonly List<ModAssemblyLoadContext> _contexts = [];
	private static bool _discoveryStarted;
	private static bool _committed;

	private static string CacheRoot => $"{Launcher.DataHead}://Cache";

	public static IReadOnlyList<ModPackage> Packages => _packages;

	public static IReadOnlyList<Mod> DiscoveredMods => [.. _discovered.Select(entry => entry.Mod)];

	public static IReadOnlyList<Mod> LoadedMods { get; private set; } = [];

	public static bool IsLoaded { get; private set; }

	public static void Discover()
	{
		if (_discoveryStarted) throw new InvalidOperationException("Mod discovery has already run.");
		_discoveryStarted = true;

		const string modRoot = $"{Launcher.ModHead}://";
		if (!FileIO.DirectoryExists(modRoot))
		{
			LogUtils.LogMessage("Mod directory does not exist, no mod was loaded.", LogUtils.LogType.Warning);
			return;
		}

		var archives = FileIO.EnumerateEntries(modRoot)
			.Where(entry => !entry.IsDirectory)
			.Where(entry => string.Equals(Path.GetExtension(entry.Path), ".zip", StringComparison.OrdinalIgnoreCase))
			.OrderBy(entry => entry.Path, StringComparer.Ordinal)
			.ToArray();

		foreach (var archive in archives)
		{
			try
			{
				LoadPackage(archive.Path);
			}
			catch(Exception e)
			{
				LogUtils.LogMessage($"Failed to load mod package '{archive.Path}': {e.Message}", LogUtils.LogType.Error);
			}
		}
	}

	public static void Commit(ModFileSystem assets)
	{
		ArgumentNullException.ThrowIfNull(assets);
		if (_committed) throw new InvalidOperationException("Mods have already been committed.");
		_committed = true;

		var rejected = ModAssetMounter.MountAll(assets, _packages);
		DiscardRejectedPackages(rejected);

		LoadRegistered();

		DropUnloadedAssetLayers(assets);
		ModAssetMounter.Seal(assets);
	}

	internal static void LoadRegistered()
	{
		if (IsLoaded) throw new InvalidOperationException("Mods are already loaded.");
		IsLoaded = true;

		ModRegistry.Freeze();
		var order = ModLoadOrder.Resolve(ModRegistry.RegisteredMods);
		LoadedMods = ModInitializer.Initialize(order);
		ContentSerializer.ResetOptions();
		ModResourceLoader.LoadAll(LoadedMods);
		ContentStore.Seal();
		ComponentManager.Seal();
		CommandRegistry.Seal();
	}

	private static void LoadPackage(string sourcePath)
	{
		var location = FileIO.Resolve(sourcePath);
		if (!location.FileSystem.TryGetPhysicalPath(location.Path, out var zipPath))
			throw new InvalidOperationException(
				$"'{Launcher.ModHead}://' is not backed by a physical file system, so mod packages cannot be opened from it.");

		var package = ModPackage.Open(zipPath);
		var contextCount = _contexts.Count;

		try
		{
			var info = package.Info
			           ?? throw new InvalidDataException($"'{Path.GetFileName(zipPath)}' has no {ModPackage.ManifestName}.");
			RegisterMods(package, CreateEntryPoints(package), info);
			_packages.Add(package);
		}
		catch
		{
			UnloadContexts(contextCount);
			package.Dispose();
			throw;
		}
	}

	private static IReadOnlyList<Mod> CreateEntryPoints(ModPackage package)
	{
		var directory = ModExtractor.Extract(package, CacheRoot);
		if (directory is null) return [new Mod()];

		var mods = ModEntryPoints.Create(package.ModId, directory, _contexts, out var hasManagedAssemblies);
		if (hasManagedAssemblies && mods.Count == 0)
			throw new InvalidDataException(
				$"'{Path.GetFileName(package.ZipPath)}' contains managed assemblies but no {nameof(Mod)} implementation.");

		return mods.Count == 0 ? [new Mod()] : mods;
	}

	private static void RegisterMods(ModPackage package, IReadOnlyList<Mod> mods, ModInfo info)
	{
		var registered = new List<Mod>(mods.Count);
		try
		{
			foreach (var mod in mods)
			{
				ModRegistry.Register(mod, info);
				registered.Add(mod);
			}
		}
		catch
		{
			foreach (var mod in registered) ModRegistry.Unregister(mod);
			throw;
		}

		foreach (var mod in registered) _discovered.Add((mod, package));
	}

	private static void DiscardRejectedPackages(IReadOnlyList<string> rejected)
	{
		if (rejected.Count == 0) return;

		LogUtils.LogMessage(
			$"These mods will not be loaded because their assets could not be mounted: {string.Join(", ", rejected)}",
			LogUtils.LogType.Error);

		foreach (var package in _packages.Where(package => rejected.Contains(package.ModId)).ToArray())
		{
			foreach (var entry in _discovered.Where(entry => ReferenceEquals(entry.Package, package)).ToArray())
			{
				ModRegistry.Unregister(entry.Mod);
				_discovered.Remove(entry);
			}

			_packages.Remove(package);
		}
	}

	private static void DropUnloadedAssetLayers(ModFileSystem assets)
	{
		foreach (var package in _packages)
		{
			var finished = _discovered.Any(entry =>
				ReferenceEquals(entry.Package, package) && LoadedMods.Contains(entry.Mod));
			if (finished) continue;

			assets.RemoveOverrideLayers(package.ModId);
			assets.UnregisterNamespace(package.ModId);
			LogUtils.LogMessage(
				$"Resources of mod '{package.ModId}' were dropped: the mod did not finish loading.",
				LogUtils.LogType.Warning);
		}
	}

	private static void UnloadContexts(int keepCount)
	{
		while (_contexts.Count > keepCount)
		{
			var context = _contexts[^1];
			_contexts.RemoveAt(_contexts.Count - 1);
			context.Unload();
		}
	}
}
