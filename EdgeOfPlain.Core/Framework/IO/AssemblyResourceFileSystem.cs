using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Reflection;
using Zio;
using Zio.FileSystems;

namespace EdgeOfPlain.Core.Framework.IO;

public sealed class AssemblyResourceFileSystem : ReadOnlyFileSystem
{
	public const string DefaultAssetFolderName = "Assets";

	private readonly Func<string, bool>? _include;
	private readonly Dictionary<string, string> _resources;
	private readonly HashSet<string> _directories;
	private readonly UPath[] _files;
	private readonly UPath[] _folders;

	public AssemblyResourceFileSystem(Assembly assembly, string? prefix = null, Func<string, bool>? include = null)
		: base(null, false)
	{
		ArgumentNullException.ThrowIfNull(assembly);

		_include = include;

		Assembly = assembly;
		Prefix = (prefix ?? $"{assembly.GetName().Name}.{DefaultAssetFolderName}").Trim('.', ' ');

		_resources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var name in assembly.GetManifestResourceNames().OrderBy(name => name, StringComparer.Ordinal))
		{
			if (!TryMap(name, out var path)) continue;
			_resources.TryAdd(path, name);
		}

		if (_resources.Count == 0)
			throw new InvalidOperationException(
				$"Assembly '{assembly.GetName().Name}' has no manifest resource under '{Prefix}'. " +
				$"Available resources ({assembly.GetManifestResourceNames().Length}): {Describe(assembly)}");

		_directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };
		foreach (var path in _resources.Keys) AddDirectoryChain(path);

		_files = [.. _resources.Keys.Select(ToAbsolute).OrderBy(path => path.FullName, StringComparer.Ordinal)];
		_folders = [.. _directories.Where(path => path.Length != 0)
			.Select(ToAbsolute)
			.OrderBy(path => path.FullName, StringComparer.Ordinal)];
	}

	public Assembly Assembly { get; }

	public string Prefix { get; }

	public string Description => $"AssemblyResource({Assembly.GetName().Name}:{Prefix})";

	protected override bool FileExistsImpl(UPath path) => _resources.ContainsKey(ToKey(path));

	protected override bool DirectoryExistsImpl(UPath path) => _directories.Contains(ToKey(path));

	protected override Stream OpenFileImpl(UPath path, FileMode mode, FileAccess access, FileShare share = FileShare.None)
	{
		if (mode != FileMode.Open || (access & FileAccess.Write) != 0) throw new IOException(FileSystemIsReadOnly);

		var key = ToKey(path);
		if (!_resources.TryGetValue(key, out var name))
			throw new FileNotFoundException(
				$"Assembly '{Assembly.GetName().Name}' has no embedded resource '{key}'", path.FullName);

		return Assembly.GetManifestResourceStream(name)
		       ?? throw new FileNotFoundException($"Embedded resource '{name}' could not be opened", path.FullName);
	}

	protected override long GetFileLengthImpl(UPath path)
	{
		using var stream = OpenFileImpl(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		return stream.Length;
	}

	protected override FileAttributes GetAttributesImpl(UPath path)
	{
		if (_resources.ContainsKey(ToKey(path))) return FileAttributes.Normal | FileAttributes.ReadOnly;
		if (_directories.Contains(ToKey(path))) return FileAttributes.Directory | FileAttributes.ReadOnly;

		throw new FileNotFoundException($"Could not find the resource `{path}`.", path.FullName);
	}

	protected override DateTime GetCreationTimeImpl(UPath path) => DefaultFileTime;

	protected override DateTime GetLastAccessTimeImpl(UPath path) => DefaultFileTime;

	protected override DateTime GetLastWriteTimeImpl(UPath path) => DefaultFileTime;

	protected override bool TryResolveLinkTargetImpl(UPath linkPath, out UPath resolvedPath)
	{
		resolvedPath = linkPath;
		return false;
	}

	protected override (IFileSystem FileSystem, UPath Path) ResolvePathImpl(UPath path) => (this, path);

	protected override IEnumerable<UPath> EnumeratePathsImpl(UPath path, string searchPattern,
		SearchOption searchOption, SearchTarget searchTarget)
	{
		var directory = ToKey(path);
		if (!_directories.Contains(directory))
			throw new DirectoryNotFoundException($"Could not find a part of the path `{path}`.");

		if (searchTarget is SearchTarget.File or SearchTarget.Both)
		{
			foreach (var file in _files)
			{
				if (!IsBelow(file, path, searchOption)) continue;
				if (!Matches(searchPattern, file.GetName())) continue;

				yield return file;
			}
		}

		if (searchTarget is SearchTarget.Directory or SearchTarget.Both)
		{
			foreach (var folder in _folders)
			{
				if (!IsBelow(folder, path, searchOption)) continue;
				if (!Matches(searchPattern, folder.GetName())) continue;

				yield return folder;
			}
		}
	}

	protected override IEnumerable<FileSystemItem> EnumerateItemsImpl(UPath path, SearchOption searchOption,
		SearchPredicate? searchPredicate)
	{
		var directory = ToKey(path);
		if (!_directories.Contains(directory))
			throw new DirectoryNotFoundException($"Could not find a part of the path `{path}`.");

		foreach (var file in _files)
		{
			if (!IsBelow(file, path, searchOption)) continue;

			var item = new FileSystemItem
			{
				FileSystem = this,
				AbsolutePath = file,
				Path = file,
				Attributes = FileAttributes.Normal | FileAttributes.ReadOnly,
				CreationTime = DefaultFileTime,
				LastWriteTime = DefaultFileTime,
				LastAccessTime = DefaultFileTime,
				Length = GetFileLengthImpl(file),
			};

			if (searchPredicate is null || searchPredicate(ref item)) yield return item;
		}

		foreach (var folder in _folders)
		{
			if (!IsBelow(folder, path, searchOption)) continue;

			var item = new FileSystemItem
			{
				FileSystem = this,
				AbsolutePath = folder,
				Path = folder,
				Attributes = FileAttributes.Directory | FileAttributes.ReadOnly,
				CreationTime = DefaultFileTime,
				LastWriteTime = DefaultFileTime,
				LastAccessTime = DefaultFileTime,
				Length = 0,
			};

			if (searchPredicate is null || searchPredicate(ref item)) yield return item;
		}
	}

	private static bool Matches(string searchPattern, string name)
		=> FileSystemName.MatchesSimpleExpression(searchPattern, name, ignoreCase: true);

	private static bool IsBelow(UPath candidate, UPath directory, SearchOption searchOption)
	{
		if (candidate == directory) return false;
		if (searchOption == SearchOption.TopDirectoryOnly) return candidate.GetDirectory() == directory;

		if (directory == UPath.Root) return true;

		return candidate.FullName.StartsWith(directory.FullName + "/", StringComparison.OrdinalIgnoreCase);
	}

	private static UPath ToAbsolute(string key) => key.Length == 0 ? UPath.Root : new UPath("/" + key);

	private static string ToKey(UPath path) => path.FullName.Trim('/');

	private void AddDirectoryChain(string path)
	{
		var separator = path.LastIndexOf('/');
		while (separator > 0)
		{
			var parent = path[..separator];
			if (!_directories.Add(parent)) return;

			separator = parent.LastIndexOf('/');
		}
	}

	private bool TryMap(string resourceName, out string path)
	{
		path = string.Empty;
		var remainder = resourceName;
		if (Prefix.Length == 0) return TryToPath(remainder, out path) && (_include is null || _include(path));
		if (!resourceName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;
		if (resourceName.Length <= Prefix.Length + 1) return false;
		var separator = resourceName[Prefix.Length];
		if (separator is not ('.' or '/')) return false;

		remainder = resourceName[(Prefix.Length + 1)..];

		return TryToPath(remainder, out path) && (_include is null || _include(path));
	}

	private static bool TryToPath(string remainder, out string path)
	{
		path = string.Empty;

		if (remainder.Contains('/') || remainder.Contains('\\'))
			return TryNormalize(remainder.Replace('\\', '/'), out path);

		var segments = remainder.Split('.', StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length < 2) return TryNormalize(remainder, out path);

		var file = $"{segments[^2]}.{segments[^1]}";
		var folders = string.Join('/', segments[..^2]);

		return TryNormalize(folders.Length == 0 ? file : $"{folders}/{file}", out path);
	}

	private static bool TryNormalize(string candidate, out string path)
	{
		path = string.Empty;

		var normalized = candidate.Replace('\\', '/').Trim('/');
		if (normalized.Length == 0) return false;
		if (normalized.Contains("//", StringComparison.Ordinal)) return false;
		if (normalized.Split('/').Any(segment => segment is "" or "." or "..")) return false;

		path = normalized;
		return true;
	}

	private static string Describe(Assembly assembly)
	{
		var names = assembly.GetManifestResourceNames();
		if (names.Length == 0) return "<none>";

		const int max = 20;
		var listed = string.Join(", ", names.Take(max));
		return names.Length <= max ? listed : $"{listed}, … (+{names.Length - max} more)";
	}
}