using System;
using System.Collections.Generic;
using System.IO;

namespace EdgeOfPlain.Core.Framework.IO;

public sealed class SubFileSystem : FileSystem
{

	public SubFileSystem(FileSystem inner, string? prefix = null)
	{
		ArgumentNullException.ThrowIfNull(inner);
		Inner = inner;
		Prefix = NormalizePath(prefix ?? string.Empty);
	}

	public FileSystem Inner { get; }

	public string Prefix { get; }

	public override bool IsReadOnly => Inner.IsReadOnly;

	public override bool IsCaseSensitive => Inner.IsCaseSensitive;

	public override bool TryGetPhysicalPath(string path, out string physicalPath)
		=> Inner.TryGetPhysicalPath(Combine(path), out physicalPath);

	protected override bool FileExistsCore(string path) => Inner.FileExists(Combine(path));

	protected override bool DirectoryExistsCore(string path) => Inner.DirectoryExists(Combine(path));

	protected override Stream OpenReadCore(string path) => Inner.OpenRead(Combine(path));

	protected override IEnumerable<FileSystemEntry> EnumerateEntriesCore(string path)
	{
		foreach (var entry in Inner.EnumerateEntries(Combine(path)))
		{
			var relative = Strip(entry.Path);
			if (relative.Length == 0) continue;

			yield return entry with { Path = relative };
		}
	}

	protected override void Dispose(bool disposing) { }

	private string Combine(string path)
	{
		var normalized = NormalizePath(path);
		if (Prefix.Length == 0) return normalized;
		return normalized.Length == 0 ? Prefix : $"{Prefix}/{normalized}";
	}

	private string Strip(string path)
	{
		if (Prefix.Length == 0) return path;
		if (path.Length == Prefix.Length) return string.Empty;

		return path.Length > Prefix.Length + 1 && path.StartsWith(Prefix, StringComparison.Ordinal) && path[Prefix.Length] == '/'
			? path[(Prefix.Length + 1)..]
			: string.Empty;
	}
}
