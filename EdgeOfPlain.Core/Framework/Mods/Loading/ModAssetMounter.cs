using System;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Utils;

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
				LogUtils.LogMessage(
					$"mod '{package.ModId}' do not contain directory '{ModPackage.AssetDirectoryName}/'.",
					LogUtils.LogType.Warning);

			try
			{
				assets.RegisterNamespace(package.ModId, package.Assets);
			}
			catch(Exception e)
			{
				LogUtils.LogMessage($"Namespace of mod '{package.ModId}' mount failed.", LogUtils.LogType.Error);
				LogUtils.LogMessage(e.Message, LogUtils.LogType.Error);
				rejected.Add(package.ModId);
			}
		}

		foreach (var package in inLoadOrder)
		{
			if (rejected.Contains(package.ModId)) continue;

			foreach (var (target, layer) in package.Overriders)
			{
				if (assets.AddOverrideLayer(target, layer, package.ModId)) continue;

				LogUtils.LogMessage(
					$"{ModPackage.OverriderDirectoryName}/{target}/ in mod '{package.ModId}' will be ignore, " +
					$"because of nonexistent target namespace",
					LogUtils.LogType.Error);
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

			LogUtils.LogMessage($"namespace '{name}': {overlay.Shadows.Count} files has been covered");

			var listed = 0;
			foreach (var (path, index) in overlay.Shadows.OrderBy(pair => pair.Key, StringComparer.Ordinal))
			{
				if (listed++ >= MaxListedShadows)
				{
					LogUtils.LogMessage($"  ...remaining {overlay.Shadows.Count - MaxListedShadows} omitted");
					break;
				}
				LogUtils.LogMessage($"  {path} ← {overlay.Layers[index].Name}");
			}

			foreach (var diagnostic in overlay.Diagnostics)
				LogUtils.LogMessage($"  {diagnostic}", LogUtils.LogType.Warning);
		}
	}
}
