using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Components;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;
using Zio;

namespace EdgeOfPlain.Core.Framework.Mods.Loading
{
	public static class ModLoader
	{
		private static readonly List<ModPackage> _packages = [];
		private static readonly List<(Mod Mod, ModPackage Package)> _discovered = [];
		private static readonly List<ModAssemblyLoadContext> _contexts = [];
		private static readonly Dictionary<string, ModAssemblyLoadContext> _contextsById = new(StringComparer.Ordinal);
		private static readonly ModAssemblyRegistry _assemblyProviders = new();
		private static bool _discoveryStarted;
		private static bool _committed;

		private static UPath CacheRoot => FileIO.Data / "Cache";

		public static IReadOnlyList<ModPackage> Packages => _packages;

		public static IReadOnlyList<Mod> DiscoveredMods => [.. _discovered.Select(entry => entry.Mod)];

		public static IReadOnlyList<Mod> LoadedMods { get; private set; } = [];

		public static bool IsLoaded { get; private set; }

		public static void Discover()
		{
			if (_discoveryStarted) throw new InvalidOperationException("Mod discovery has already run.");
			Globals.ModChecksum = null;
			_discoveryStarted = true;

			var fileSystem = FileIO.Root;
			if (!fileSystem.DirectoryExists(FileIO.Mods))
			{
				LogUtils.Logger(typeof(ModLoader)).LogWarning("Mod directory does not exist, no mod was loaded.");
				return;
			}

			var archives = fileSystem.EnumerateFiles(FileIO.Mods, "*", SearchOption.TopDirectoryOnly)
				.Where(path => string.Equals(path.GetExtensionWithDot(), ".zip", StringComparison.OrdinalIgnoreCase))
				.OrderBy(path => path.FullName, StringComparer.Ordinal)
				.ToArray();

			var discovered = new List<(ModPackage Package, ModInfo Info)>(archives.Length);

			foreach (var archive in archives)
			{
				try
				{
					discovered.Add(OpenPackage(archive));
				}
				catch(Exception e)
				{
					LogUtils.Logger(typeof(ModLoader)).LogError("Failed to load mod package '{ArchivePath}': {EMessage}",
						archive.FullName, e.Message);
				}
			}

			foreach (var entry in ModLoadOrder.OrderInfos(discovered, static entry => entry.Info))
			{
				try
				{
					LoadPackage(entry.Package, entry.Info);
				}
				catch(Exception e)
				{
					LogUtils.Logger(typeof(ModLoader)).LogError("Failed to load mod package '{ArchivePath}': {EMessage}",
						entry.Package.ZipPath, e.Message);
				}
			}

			LogUtils.Logger(typeof(ModLoader)).LogInformation(
				"Discovered {Count} mod package(s).", _packages.Count);
		}

		public static void Commit(ModFileSystem assets)
		{
			ArgumentNullException.ThrowIfNull(assets);
			if (_committed) throw new InvalidOperationException("Mods have already been committed.");
			Globals.ModChecksum = null;
			_committed = true;

			var rejected = ModAssetMounter.MountAll(assets, _packages);
			DiscardRejectedPackages(rejected);

			LoadRegistered();

			DropUnloadedAssetLayers(assets);
			ModAssetMounter.Seal(assets);
			var activePackages = _packages.Where(package => _discovered.Any(entry =>
				ReferenceEquals(entry.Package, package) && LoadedMods.Contains(entry.Mod))).ToArray();
			Globals.ModChecksum = ModChecksum.Compute(assets, LoadedMods, activePackages);
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
			if (FileIO.TryGetMounted(FileIO.Assets, out var mounted) && mounted is ModFileSystem assets)
				Globals.ModChecksum = ModChecksum.Compute(assets, LoadedMods, []);

			LogUtils.Logger(typeof(ModLoader)).LogInformation(
				"All mods finished loading: {Loaded} of {Registered} registered mods succeeded; load order: {Order}.",
				LoadedMods.Count, ModRegistry.RegisteredMods.Count,
				string.Join(" → ", order.Select(mod => mod.ModInfo.Id)));
		}

		private static (ModPackage Package, ModInfo Info) OpenPackage(UPath sourcePath)
		{
			if (!FileIO.Root.TryGetPhysicalPath(sourcePath, out var zipPath))
				throw new InvalidOperationException(
					$"`{sourcePath}` is not backed by a physical file system, so mod packages cannot be opened from it.");

			ModPackage? package = null;

			try
			{
				package = ModPackage.Open(zipPath);
				var info = package.Info
				           ?? throw new InvalidDataException($"'{Path.GetFileName(zipPath)}' has no {ModPackage.ManifestName}.");

				return (package, info);
			}
			catch
			{
				package?.Dispose();
				throw;
			}
		}

		private static void LoadPackage(ModPackage package, ModInfo info)
		{
			var contextCount = _contexts.Count;

			try
			{
				var mods = CreateEntryPoints(package, info, out var context);
				RegisterMods(package, mods, info);
				if (context is not null) _contextsById[context.ModId] = context;
				_packages.Add(package);

				LogUtils.Logger(typeof(ModLoader)).LogInformation(
					"Mod package '{FileName}' loaded as '{ModId}'.", Path.GetFileName(package.ZipPath), info.Id);
			}
			catch
			{
				DiscardContexts(contextCount);
				package.Dispose();
				throw;
			}
		}

		private static IReadOnlyList<Mod> CreateEntryPoints(
			ModPackage package,
			ModInfo info,
			out ModAssemblyLoadContext? context)
		{
			context = null;

			var directory = ModExtractor.Extract(package, FileIO.Root, CacheRoot);
			if (directory is null) return [new Mod()];

			var providers = new List<ModAssemblyLoadContext>();
			if (info.Dependence is { } dependence)
			{
				foreach (var required in dependence)
				{
					if (string.IsNullOrEmpty(required.Id)) continue;
					if (_contextsById.TryGetValue(required.Id, out var provider)) providers.Add(provider);
				}
			}

			var mods = ModEntryPoints.Create(
				package.ModId, directory, providers, _assemblyProviders, _contexts,
				out var hasManagedAssemblies, out context);

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

			LogUtils.Logger(typeof(ModLoader)).LogError(
				"These mods will not be loaded because their assets could not be mounted: {Join}", string.Join(", ", rejected));

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
				LogUtils.Logger(typeof(ModLoader)).LogWarning(
					"Resources of mod '{PackageModId}' were dropped: the mod did not finish loading.", package.ModId);
			}
		}

		private static void DiscardContexts(int keepCount)
		{
			while (_contexts.Count > keepCount)
			{
				var context = _contexts[^1];
				_contexts.RemoveAt(_contexts.Count - 1);
				_contextsById.Remove(context.ModId);
				_assemblyProviders.Remove(context);
				context.Unload();
			}
		}
	}
}