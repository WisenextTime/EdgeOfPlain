using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Utils;

namespace EdgeOfPlain.Core.Framework.Mods;

public sealed class ModLoader(ModFileSystem assets) : IDisposable
{
	private readonly List<ModPackage> _packages = [];
	private readonly List<(IMod Mod, ModPackage Package)> _mods = [];
	private readonly List<ModLoadContext> _contexts = [];

	public string CacheRoot { get; init; } = $"{Launcher.DataHead}://cache";

	public IReadOnlyList<ModPackage> Packages => _packages;

	public IReadOnlyList<IMod> Mods => _mods.Select(entry => entry.Mod).ToArray();

	public void Discover()
	{
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
				LoadPackage($"{Launcher.ModHead}://{entry.Path}");
			}
			catch (Exception e)
			{
				LogUtils.LogMessage($"Failed to load mod package '{entry.Path}': {e.Message}", LogUtils.LogType.Error);
			}
		}
	}

	public void Commit()
	{
		ModAssets.MountAll(assets, _packages);

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

	public void Dispose()
	{
		foreach (var context in _contexts) context.Unload();
		_contexts.Clear();

		foreach (var package in _packages) package.Dispose();
		_packages.Clear();
		_mods.Clear();
	}

	private void LoadPackage(string sourcePath)
	{
		var location = FileIO.Resolve(sourcePath);
		if (!location.FileSystem.TryGetPhysicalPath(location.Path, out var zipPath))
			throw new InvalidOperationException(
				$"'{Launcher.ModHead}://' is not backed by a physical file system, so mod packages cannot be opened from it.");

		var package = ModPackage.Open(zipPath);
		try
		{
			var info = package.Info
				?? throw new InvalidDataException($"'{Path.GetFileName(zipPath)}' has no {ModPackage.ManifestName}.");

			var directory = ModExtractor.Extract(package, CacheRoot);
			IReadOnlyList<IMod> mods = [];

			if (directory is not null)
			{
				mods = ModEntryPoints.Create(package.ModId, directory, _contexts, out var hasManagedAssemblies);
				if (hasManagedAssemblies && mods.Count == 0)
					throw new InvalidDataException(
						$"'{Path.GetFileName(zipPath)}' contains managed assemblies but no {nameof(IMod)} implementation.");
			}

			if (mods.Count == 0) mods = [new TomlMod(package)];

			var registered = new List<IMod>();
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
			package.Dispose();
			throw;
		}
	}
}
