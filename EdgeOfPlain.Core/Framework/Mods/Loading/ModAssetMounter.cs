using System;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;

namespace EdgeOfPlain.Core.Framework.Mods.Loading;

public static class ModAssetMounter
{
	private const int MaxListedShadows = 20;

	public static IReadOnlyList<string> MountAll(ModFileSystem assets, IReadOnlyList<ModPackage> inLoadOrder)
	{
		ArgumentNullException.ThrowIfNull(assets);
		ArgumentNullException.ThrowIfNull(inLoadOrder);

		var rejected = new List<string>();

		foreach (var package in inLoadOrder)
		{
			if (!package.HasAssets)
				LogUtils.Logger(typeof(ModAssetMounter)).LogWarning(
					"mod '{PackageModId}' do not contain directory '{AssetDirectoryName}/'.",
					package.ModId, ModPackage.AssetDirectoryName);

			try
			{
				assets.RegisterNamespace(package.ModId, package.Assets);
			}
			catch(Exception e)
			{
				LogUtils.Logger(typeof(ModAssetMounter))
					.LogError("Namespace of mod '{PackageModId}' mount failed.", package.ModId);
				LogUtils.Logger(typeof(ModAssetMounter)).LogError("{error}", e.Message);
				rejected.Add(package.ModId);
			}
		}

		foreach (var package in inLoadOrder)
		{
			if (rejected.Contains(package.ModId)) continue;

			foreach (var (target, layer) in package.Overriders)
			{
				if (assets.AddOverrideLayer(target, layer, package.ModId)) continue;

				LogUtils.Logger(typeof(ModAssetMounter)).LogError(
					"{OverriderDirectoryName}/{Target}/ in mod '{PackageModId}' will be ignore, " +
					"because of nonexistent target namespace", ModPackage.OverriderDirectoryName, target, package.ModId);
			}
		}

		return rejected;
	}

	public static void Seal(ModFileSystem assets)
	{
		ArgumentNullException.ThrowIfNull(assets);

		assets.Seal();
		LogManifest(assets);
	}

	private static void LogManifest(ModFileSystem assets)
	{
		foreach (var (name, overlay) in assets.Namespaces.OrderBy(pair => pair.Key, StringComparer.Ordinal))
		{
			if (overlay.Shadows.Count == 0 && overlay.Diagnostics.Count == 0) continue;

			LogUtils.Logger(typeof(ModAssetMounter)).LogInformation("namespace '{Name}': {ShadowsCount} files has been covered",
				name, overlay.Shadows.Count);

			var listed = 0;
			foreach (var (path, index) in overlay.Shadows.OrderBy(pair => pair.Key, StringComparer.Ordinal))
			{
				if (listed++ >= MaxListedShadows)
				{
					LogUtils.Logger(typeof(ModAssetMounter)).LogInformation("  ...remaining {MaxListedShadows} omitted",
						overlay.Shadows.Count - MaxListedShadows);
					break;
				}
				LogUtils.Logger(typeof(ModAssetMounter)).LogInformation("  {Path} ← {Name}", path, overlay.Layers[index].Name);
			}

			foreach (var diagnostic in overlay.Diagnostics)
				LogUtils.Logger(typeof(ModAssetMounter)).LogWarning("  {Diagnostic}", diagnostic);
		}
	}
}