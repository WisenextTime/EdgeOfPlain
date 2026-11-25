using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Enumeration;
using Zio;
using Zio.FileSystems;

namespace EdgeOfPlain.Core.Framework.IO;

/// <summary>
/// <see cref="ZipArchiveFileSystem"/> keeps only the entries the archive explicitly stores,
/// so archives written without folder records would look flat; mod packages rely on folder enumeration.
/// </summary>
public sealed class ZipFileSystem : ReadOnlyFileSystem
{
	private readonly ZipArchiveFileSystem _zip;
	private readonly bool _caseSensitive;
	private readonly UPathComparer _comparer;
	private readonly HashSet<UPath> _files;
	private readonly HashSet<UPath> _directories = [UPath.Root];
	private readonly Dictionary<UPath, List<UPath>> _children = [];

	public ZipFileSystem(string path, bool isCaseSensitive = false)
		: base(null, false)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		_caseSensitive = isCaseSensitive;
		_comparer = isCaseSensitive ? UPathComparer.Ordinal : UPathComparer.OrdinalIgnoreCase;
		_files = new HashSet<UPath>(_comparer);
		_zip = new ZipArchiveFileSystem(path, ZipArchiveMode.Read, leaveOpen: false, isCaseSensitive);

		try
		{
			foreach (var file in _zip.EnumerateFiles(UPath.Root, "*", SearchOption.AllDirectories)) AddFile(file);
		}
		catch
		{
			_zip.Dispose();
			throw;
		}
	}

	public bool IsCaseSensitive => _caseSensitive;

	public string Description => "Zip";

	public IReadOnlyCollection<UPath> Files => _files;

	protected override bool FileExistsImpl(UPath path) => _files.Contains(path);

	protected override bool DirectoryExistsImpl(UPath path) => _directories.Contains(path);

	protected override Stream OpenFileImpl(UPath path, FileMode mode, FileAccess access, FileShare share = FileShare.None)
	{
		if (mode != FileMode.Open || (access & FileAccess.Write) != 0) throw new IOException(FileSystemIsReadOnly);

		return !_files.Contains(path)
			? throw new FileNotFoundException($"`{path}` do not exist in the archive", path.FullName)
			: _zip.OpenFile(path, FileMode.Open, FileAccess.Read, share);
	}

	protected override long GetFileLengthImpl(UPath path) => _zip.GetFileLength(path);

	protected override FileAttributes GetAttributesImpl(UPath path)
	{
		if (_files.Contains(path)) return FileAttributes.ReadOnly | FileAttributes.Normal;
		if (_directories.Contains(path)) return FileAttributes.ReadOnly | FileAttributes.Directory;
		throw new FileNotFoundException($"`{path}` do not exist in the archive", path.FullName);
	}

	protected override DateTime GetCreationTimeImpl(UPath path) => DefaultFileTime;

	protected override DateTime GetLastAccessTimeImpl(UPath path) => DefaultFileTime;

	protected override DateTime GetLastWriteTimeImpl(UPath path) => DefaultFileTime;

	protected override bool TryResolveLinkTargetImpl(UPath path, out UPath linkTarget)
	{
		linkTarget = UPath.Empty;
		return false;
	}

	protected override (IFileSystem FileSystem, UPath Path) ResolvePathImpl(UPath path) => (this, path);

	protected override IEnumerable<UPath> EnumeratePathsImpl(UPath path, string searchPattern, SearchOption searchOption,
		SearchTarget searchTarget)
	{
		if (!_directories.Contains(path)) throw new DirectoryNotFoundException($"Could not find a part of the path `{path}`.");

		var includeFiles = searchTarget is SearchTarget.File or SearchTarget.Both;
		var includeDirectories = searchTarget is SearchTarget.Directory or SearchTarget.Both;

		foreach (var child in EnumerateChildren(path, searchOption))
		{
			var isFile = _files.Contains(child);
			if (isFile ? !includeFiles : !includeDirectories) continue;
			if (!FileSystemName.MatchesSimpleExpression(searchPattern, child.GetName(), ignoreCase: !_caseSensitive)) continue;

			yield return child;
		}
	}

	protected override IEnumerable<FileSystemItem> EnumerateItemsImpl(UPath path, SearchOption searchOption,
		SearchPredicate? searchFilter)
	{
		if (!_directories.Contains(path)) throw new DirectoryNotFoundException($"Could not find a part of the path `{path}`.");

		foreach (var child in EnumerateChildren(path, searchOption))
		{
			var isFile = _files.Contains(child);
			var item = new FileSystemItem
			{
				FileSystem = this,
				Path = child,
				AbsolutePath = child,
				Attributes = isFile
					? FileAttributes.ReadOnly | FileAttributes.Normal
					: FileAttributes.ReadOnly | FileAttributes.Directory,
				CreationTime = DefaultFileTime,
				LastAccessTime = DefaultFileTime,
				LastWriteTime = DefaultFileTime,
				Length = isFile ? _zip.GetFileLength(child) : 0,
			};

			if (searchFilter is not null && !searchFilter(ref item)) continue;

			yield return item;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing) _zip.Dispose();
		base.Dispose(disposing);
	}

	private IEnumerable<UPath> EnumerateChildren(UPath path, SearchOption searchOption)
	{
		if (!_children.TryGetValue(path, out var children)) yield break;

		foreach (var child in children) yield return child;
		if (searchOption != SearchOption.AllDirectories) yield break;

		foreach (var child in children)
		{
			if (_files.Contains(child)) continue;

			foreach (var descendant in EnumerateChildren(child, searchOption)) yield return descendant;
		}
	}

	private void AddFile(UPath file)
	{
		if (!_files.Add(file)) return;

		var directory = file.GetDirectory();
		AddChild(directory, file);

		while (_directories.Add(directory))
		{
			if (directory == UPath.Root) break;

			var parent = directory.GetDirectory();
			AddChild(parent, directory);
			directory = parent;
		}
	}

	private void AddChild(UPath directory, UPath child)
	{
		if (!_children.TryGetValue(directory, out var children)) _children[directory] = children = [];
		if (!children.Contains(child)) children.Add(child);
	}
}