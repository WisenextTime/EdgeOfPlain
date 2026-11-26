using System;
using System.IO;
using System.Linq;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;
using Zio;

namespace EdgeOfPlain.Core.Framework.Mods.Loading
{
	public static class ModExtractor
	{
		private const string CompleteMarker = ".complete";

		public static string? Extract(ModPackage package, IFileSystem fileSystem, UPath cacheRoot)
		{
			ArgumentNullException.ThrowIfNull(package);
			ArgumentNullException.ThrowIfNull(fileSystem);

			var files = package.Root
				.EnumerateFiles(UPath.Root, "*", SearchOption.AllDirectories)
				.Where(ShouldExtract)
				.ToArray();

			if (files.Length == 0) return null;

			var archive = new FileInfo(package.ZipPath);
			var stamp = $"{package.Info?.Version?.ToString() ?? "0.0"}-{archive.Length}-{archive.LastWriteTimeUtc.Ticks}";
			var target = cacheRoot / package.ModId / stamp;

			if (!fileSystem.TryGetPhysicalPath(target, out var directory))
				throw new InvalidOperationException(
					$"`{cacheRoot}` is not backed by a physical file system, so mod assemblies cannot be loaded from it.");

			var markerPath = target / CompleteMarker;
			if (fileSystem.FileExists(markerPath))
			{
				LogUtils.Logger(typeof(ModExtractor)).LogInformation(
					"Reusing cached assemblies of mod '{ModId}' at '{Target}'.", package.ModId, target);
				return directory;
			}

			foreach (var file in files)
			{
				var destination = target / file.FullName.TrimStart('/');
				using var source = package.Root.OpenReadShared(file);
				fileSystem.CreateParentDirectory(destination);
				using var output = fileSystem.OpenFile(destination, FileMode.Create, FileAccess.Write);
				source.CopyTo(output);
			}

			fileSystem.CreateParentDirectory(markerPath);
			using (var marker = fileSystem.OpenFile(markerPath, FileMode.Create, FileAccess.Write))
			using (var writer = new StreamWriter(marker))
				writer.Write(stamp);

			LogUtils.Logger(typeof(ModExtractor)).LogInformation(
				"Extracted {FileCount} files of mod '{ModId}' to '{Target}'.", files.Length, package.ModId, target);
			return directory;
		}

		private static bool ShouldExtract(UPath path)
		{
			var fullName = path.FullName;
			return fullName.StartsWith("/runtimes/", StringComparison.OrdinalIgnoreCase) ||
			       fullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
			       fullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
			       fullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
			       fullName.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
			       fullName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase);
		}
	}
}