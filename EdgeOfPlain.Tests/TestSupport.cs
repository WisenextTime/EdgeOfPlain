using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EdgeOfPlain.Core.Framework.IO;

namespace EdgeOfPlain.Tests;

internal sealed class TempDirectory : IDisposable
{
	public string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "test-temp", Guid.NewGuid().ToString("N"));

	public TempDirectory() => Directory.CreateDirectory(Path);

	public string Write(string relative, string content)
	{
		var full = System.IO.Path.Combine(Path, relative);
		Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
		File.WriteAllText(full, content);
		return full;
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(Path, true);
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
	}
}

internal sealed class MemoryFileSystem(bool caseSensitive = true) : FileSystem
{
	private readonly Dictionary<string, byte[]> _files = new(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _directories = new(StringComparer.Ordinal) { string.Empty };

	public override bool IsCaseSensitive { get; } = caseSensitive;

	public override bool IsReadOnly => false;

	public IReadOnlyDictionary<string, byte[]> Files => _files;

	public MemoryFileSystem Add(string path, string content) => Add(path, Encoding.UTF8.GetBytes(content));

	public MemoryFileSystem Add(string path, byte[] content)
	{
		var normalized = NormalizePath(path);
		_files[normalized] = content;
		AddParentDirectories(normalized);
		return this;
	}

	protected override bool FileExistsCore(string path) => path.Length != 0 && _files.ContainsKey(path);

	protected override bool DirectoryExistsCore(string path) => _directories.Contains(path);

	protected override Stream OpenReadCore(string path)
		=> _files.TryGetValue(path, out var content)
			? new MemoryStream(content, writable: false)
			: throw new FileNotFoundException($"'{path}' does not exist", path);

	protected override Stream OpenWriteCore(string path)
		=> new CollectingStream(content =>
		{
			_files[path] = content;
			AddParentDirectories(path);
		});

	protected override void DeleteFileCore(string path)
	{
		if (!_files.Remove(path)) throw new FileNotFoundException($"'{path}' does not exist", path);
	}

	protected override void CreateDirectoryCore(string path)
	{
		if (path.Length != 0) _directories.Add(path);
		AddParentDirectories(path);
	}

	protected override void DeleteDirectoryCore(string path, bool recursive)
	{
		if (!recursive)
		{
			foreach (var file in _files.Keys)
				if (Parent(file) == path)
					throw new IOException($"directory '{path}' is not empty");

			_directories.Remove(path);
			return;
		}

		var prefix = path.Length == 0 ? string.Empty : path + "/";
		foreach (var file in new List<string>(_files.Keys))
			if (file.StartsWith(prefix, StringComparison.Ordinal))
				_files.Remove(file);

		_directories.RemoveWhere(directory => directory.StartsWith(prefix, StringComparison.Ordinal) || directory == path);
	}

	protected override IEnumerable<FileSystemEntry> EnumerateEntriesCore(string path)
	{
		var entries = new List<FileSystemEntry>();

		foreach (var file in _files.Keys)
			if (Parent(file) == path)
				entries.Add(new FileSystemEntry(file, false));

		foreach (var directory in _directories)
			if (directory.Length != 0 && Parent(directory) == path)
				entries.Add(new FileSystemEntry(directory, true));

		entries.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));
		return entries;
	}

	private void AddParentDirectories(string path)
	{
		var index = path.LastIndexOf('/');
		while (index > 0)
		{
			var parent = path[..index];
			if (!_directories.Add(parent)) return;
			index = parent.LastIndexOf('/');
		}
	}

	private static string Parent(string path)
	{
		var index = path.LastIndexOf('/');
		return index < 0 ? string.Empty : path[..index];
	}

	private sealed class CollectingStream(Action<byte[]> collect) : MemoryStream
	{
		protected override void Dispose(bool disposing)
		{
			if (disposing) collect(ToArray());
			base.Dispose(disposing);
		}
	}
}
