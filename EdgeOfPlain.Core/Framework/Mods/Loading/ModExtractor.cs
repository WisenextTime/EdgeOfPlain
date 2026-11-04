using System;
using System.IO;
using System.Linq;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;

namespace EdgeOfPlain.Core.Framework.Mods.Loading;

public static class ModExtractor
{
	private const string CompleteMarker = ".complete";

	public static string? Extract(ModPackage package, string cacheRoot)
	{
		ArgumentNullException.ThrowIfNull(package);
		ArgumentException.ThrowIfNullOrWhiteSpace(cacheRoot);

		var files = package.Root
			.EnumerateFiles(string.Empty, "*", SearchOption.AllDirectories)
			.Where(ShouldExtract)
			.ToArray();

		if (files.Length == 0) return null;

		var archive = new FileInfo(package.ZipPath);
		var stamp = $"{package.Info?.Version?.ToString() ?? "0.0"}-{archive.Length}-{archive.LastWriteTimeUtc.Ticks}";
		var location = FileIO.Resolve(cacheRoot);
		var target = location.Path.Length == 0
			? $"{package.ModId}/{stamp}"
			: $"{location.Path}/{package.ModId}/{stamp}";

		if (!location.FileSystem.TryGetPhysicalPath(target, out var directory))
			throw new InvalidOperationException(
				$"'{cacheRoot}' is not backed by a physical file system, so mod assemblies cannot be loaded from it.");

		if (location.FileSystem.FileExists($"{target}/{CompleteMarker}"))
		{
			LogUtils.Logger(typeof(ModExtractor)).LogInformation(
				"Reusing cached assemblies of mod '{ModId}' at '{Target}'.", package.ModId, target);
			return directory;
		}

		foreach (var file in files)
		{
			using var source = package.Root.OpenRead(file);
			using var destination = location.FileSystem.OpenWrite($"{target}/{file}");
			source.CopyTo(destination);
		}

		location.FileSystem.WriteAllText($"{target}/{CompleteMarker}", stamp);

		LogUtils.Logger(typeof(ModExtractor)).LogInformation(
			"Extracted {FileCount} files of mod '{ModId}' to '{Target}'.", files.Length, package.ModId, target);
		return directory;
	}

	private static bool ShouldExtract(string path)
		=> path.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase) ||
		   path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
		   path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
		   path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
		   path.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
		   path.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase);


}