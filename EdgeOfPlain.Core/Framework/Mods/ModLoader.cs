using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Utils;

namespace EdgeOfPlain.Core.Framework.Mods;

public static class ModLoader
{
	private static readonly List<ModPackage> _packages = [];
	private static readonly List<(Mod Mod, ModPackage Package)> _mods = [];
	private static readonly List<ModLoadContext> _contexts = [];
	private static bool _discovered;
	private static bool _committed;

	public static string CacheRoot => $"{Launcher.DataHead}://Cache";

	public static IReadOnlyList<ModPackage> Packages => _packages;

	public static IReadOnlyList<Mod> Mods => [.. _mods.Select(entry => entry.Mod)];

	public static void Discover()
	{
		if (_discovered) throw new InvalidOperationException("Mod discovery has already run.");
		_discovered = true;

		if (!FileIO.DirectoryExists($"{Launcher.ModHead}://"))
		{
			LogUtils.LogMessage($"Mod directory does not exist, no mod was loaded.", LogUtils.LogType.Warning);
			return;
		}

		var archives = FileIO.EnumerateEntries($"{Launcher.ModHead}://")
			.Where(entry => !entry.IsDirectory)
			.Where(entry => string.Equals(Path.GetExtension(entry.Path), ".zip", StringComparison.OrdinalIgnoreCase))
			.OrderBy(entry => entry.Path, StringComparer.Ordinal)
			.ToArray();

		foreach (var entry in archives)
		{
			try
			{
				LoadPackage(entry.Path);
			}
			catch (Exception e)
			{
				LogUtils.LogMessage($"Failed to load mod package '{entry.Path}': {e.Message}", LogUtils.LogType.Error);
			}
		}
	}

	public static void Commit(ModFileSystem assets)
	{
		if (_committed) throw new InvalidOperationException("Mods have already been committed.");
		_committed = true;

		var rejected = ModAssets.MountAll(assets, _packages);
		if (rejected.Count > 0)
		{
			LogUtils.LogMessage(
				$"These mods will not be loaded because their assets could not be mounted: {string.Join(", ", rejected)}",
				LogUtils.LogType.Error);

			foreach (var package in _packages.Where(package => rejected.Contains(package.ModId)))
			{
				foreach (var entry in _mods.Where(entry => ReferenceEquals(entry.Package, package)).ToArray())
				{
					ModManager.UnregisterMod(entry.Mod);
					_mods.Remove(entry);
				}
			}
		}

		ModManager.LoadAllMods();

		var loaded = ModManager.LoadedMods;
		foreach (var package in _packages.Where(package
			         => !_mods.Any(entry => ReferenceEquals(entry.Package, package) && loaded.Contains(entry.Mod))))
		{
			assets.RemoveOverrideLayers(package.ModId);
			assets.UnregisterNamespace(package.ModId);
			LogUtils.LogMessage(
				$"Resources of mod '{package.ModId}' were dropped: the mod did not finish loading.",
				LogUtils.LogType.Warning);
		}

		ModAssets.Seal(assets);
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

			var directory = ModExtractor.Extract(package, CacheRoot);
			IReadOnlyList<Mod> mods = [];

			if (directory is not null)
			{
				mods = ModEntryPoints.Create(package.ModId, directory, _contexts, out var hasManagedAssemblies);
				if (hasManagedAssemblies && mods.Count == 0)
					throw new InvalidDataException(
						$"'{Path.GetFileName(zipPath)}' contains managed assemblies but no {nameof(Mod)} implementation.");
			}

			if (mods.Count == 0) mods = [new Mod()];

			var registered = new List<Mod>();
			try
			{
				foreach (var mod in mods)
				{
					ModManager.RegisterMod(mod, info);
					registered.Add(mod);
				}
			}
			catch
			{
				foreach (var mod in registered) ModManager.UnregisterMod(mod);
				throw;
			}

			foreach (var mod in registered) _mods.Add((mod, package));
			_packages.Add(package);
		}
		catch
		{
			while (_contexts.Count > contextCount)
			{
				var extra = _contexts[^1];
				_contexts.RemoveAt(_contexts.Count - 1);
				extra.Unload();
			}

			package.Dispose();
			throw;
		}
	}
}
