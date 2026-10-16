using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace EdgeOfPlain.Core.Framework.IO;

public sealed class ZipFileSystem : FileSystem
{
	public const long MaxEntryLength = 512L * 1024 * 1024;

	private readonly ZipArchive _archive;
	private readonly FrozenDictionary<string, ZipArchiveEntry> _files;
	private readonly FrozenSet<string> _directories;
	private readonly FrozenDictionary<string, FileSystemEntry[]> _children;
	private readonly Lock _gate = new();

	public ZipFileSystem(string zipPath)
		: this(new ZipArchive(File.OpenRead(zipPath), ZipArchiveMode.Read, leaveOpen: false)) { }

	public ZipFileSystem(Stream source, bool leaveOpen = false)
		: this(new ZipArchive(source, ZipArchiveMode.Read, leaveOpen)) { }

	private ZipFileSystem(ZipArchive archive)
	{
		_archive = archive;

		var files = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
		var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var entry in _archive.Entries.OrderBy(entry => entry.FullName, StringComparer.Ordinal))
		{
			if (!TryNormalize(entry.FullName, out var path) || path.Length == 0) continue;

			if (entry.FullName.EndsWith('/') || entry.Name.Length == 0) directories.Add(path);
			else files.TryAdd(path, entry);
		}

		foreach (var path in files.Keys.ToArray()) ResourcePathTree.AddDirectoryChain(directories, path);
		directories.Add(string.Empty);

		var children = new Dictionary<string, List<FileSystemEntry>>(StringComparer.OrdinalIgnoreCase);
		foreach (var path in directories)
			if (path.Length != 0)
				ResourcePathTree.AddChild(children, path, isDirectory: true);

		foreach (var path in files.Keys) ResourcePathTree.AddChild(children, path, isDirectory: false);

		_files = files.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
		_directories = directories.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
		_children = children.ToFrozenDictionary(
			pair => pair.Key,
			pair => pair.Value.OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray(),
			StringComparer.OrdinalIgnoreCase);
	}

	public override bool IsReadOnly => true;

	public override bool IsCaseSensitive => false;

	protected override bool FileExistsCore(string path) => path.Length != 0 && _files.ContainsKey(path);

	protected override bool DirectoryExistsCore(string path) => _directories.Contains(path);

	protected override Stream OpenReadCore(string path)
	{
		if (!_files.TryGetValue(path, out var entry))
			throw new FileNotFoundException($"ZIP with no entry '{path}'", path);

		lock (_gate)
		{
			using var source = entry.Open();
			var buffer = new MemoryStream(entry.Length is > 0 and <= int.MaxValue ? (int)entry.Length : 0);
			var chunk = new byte[81920];
			long copied = 0;

			int read;
			while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
			{
				copied += read;
				if (copied > MaxEntryLength)
					throw new InvalidDataException($"ZIP entry '{path}' size exceeds {MaxEntryLength} byte");

				buffer.Write(chunk, 0, read);
			}

			buffer.Position = 0;
			return buffer;
		}
	}

	protected override IEnumerable<FileSystemEntry> EnumerateEntriesCore(string path)
		=> _children.TryGetValue(path, out var children) ? children : [];

	protected override void Dispose(bool disposing)
	{
		if (disposing) _archive.Dispose();
	}

	private static bool TryNormalize(string entryName, out string path)
	{
		path = string.Empty;
		try
		{
			path = NormalizePath(entryName.Replace('\\', '/'));
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}
}
