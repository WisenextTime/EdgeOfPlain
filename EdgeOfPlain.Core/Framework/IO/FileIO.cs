using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EdgeOfPlain.Core.Framework.IO;

public readonly record struct ResourceLocation(FileSystem FileSystem, string Path);

public static class FileIO
{
	public const string SchemeSeparator = "://";

	private static readonly Lock _gate = new();
	private static readonly Dictionary<string, Lazy<FileSystem>> _registry = new(StringComparer.Ordinal);
	private static FrozenDictionary<string, Lazy<FileSystem>> _mounts = FrozenDictionary<string, Lazy<FileSystem>>.Empty;

	public static IReadOnlyCollection<string> RegisteredHeads => _mounts.Keys;

	public static void RegisterFileSystem(string head, FileSystem fileSystem)
	{
		ArgumentNullException.ThrowIfNull(fileSystem);
		RegisterFileSystem(head, () => fileSystem);
	}

	public static void RegisterFileSystem(string head, Func<FileSystem> factory)
	{
		ValidateHead(head);
		ArgumentNullException.ThrowIfNull(factory);

		lock (_gate)
		{
			if (!_registry.TryAdd(head, new Lazy<FileSystem>(factory, LazyThreadSafetyMode.ExecutionAndPublication)))
				throw new ArgumentException($"head '{head}' is already registered", nameof(head));
			_mounts = _registry.ToFrozenDictionary(StringComparer.Ordinal);
		}
	}

	public static bool IsRegistered(string head)
	{
		ArgumentNullException.ThrowIfNull(head);
		return _mounts.ContainsKey(head);
	}

	public static bool UnregisterFileSystem(string head)
	{
		ArgumentNullException.ThrowIfNull(head);

		lock (_gate)
		{
			if (!_registry.Remove(head)) return false;

			_mounts = _registry.ToFrozenDictionary(StringComparer.Ordinal);
			return true;
		}
	}

	public static void Clear()
	{
		lock (_gate)
		{
			foreach (var mount in _registry.Values.Where(mount => mount.IsValueCreated)) mount.Value.Dispose();
			_registry.Clear();
			_mounts = FrozenDictionary<string, Lazy<FileSystem>>.Empty;
		}
	}

	public static bool TryResolve(string? sourcePath, out ResourceLocation location)
	{
		location = default;
		if (string.IsNullOrWhiteSpace(sourcePath)) return false;

		var separator = sourcePath.IndexOf(SchemeSeparator, StringComparison.Ordinal);
		if (separator <= 0) return false;

		if (!_mounts.TryGetValue(sourcePath[..separator], out var mount)) return false;

		location = new ResourceLocation(mount.Value, sourcePath[(separator + SchemeSeparator.Length)..]);
		return true;
	}

	public static ResourceLocation Resolve(string sourcePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
		if (TryResolve(sourcePath, out var location)) return location;

		var separator = sourcePath.IndexOf(SchemeSeparator, StringComparison.Ordinal);
		throw separator switch
		{
			< 0 => new ArgumentException($"resource path '{sourcePath}' has no '{SchemeSeparator}' head", nameof(sourcePath)),
			0 => new ArgumentException($"resource path '{sourcePath}' has an empty head", nameof(sourcePath)),
			_ => new ArgumentException($"no file system is registered for head '{sourcePath[..separator]}'", nameof(sourcePath)),
		};
	}

	public static bool FileExists(string sourcePath)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.FileExists(path);
	}

	public static bool DirectoryExists(string sourcePath)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.DirectoryExists(path);
	}

	public static bool Exists(string sourcePath)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.FileExists(path) || fileSystem.DirectoryExists(path);
	}

	public static Stream OpenRead(string sourcePath)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.OpenRead(path);
	}

	public static ValueTask<Stream> OpenReadAsync(string sourcePath, CancellationToken cancellationToken = default)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.OpenReadAsync(path, cancellationToken);
	}

	public static Stream OpenWrite(string sourcePath)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.OpenWrite(path);
	}

	public static ValueTask<Stream> OpenWriteAsync(string sourcePath, CancellationToken cancellationToken = default)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.OpenWriteAsync(path, cancellationToken);
	}

	public static byte[] ReadAllBytes(string sourcePath)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.ReadAllBytes(path);
	}

	public static ValueTask<byte[]> ReadAllBytesAsync(string sourcePath, CancellationToken cancellationToken = default)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.ReadAllBytesAsync(path, cancellationToken);
	}

	public static void WriteAllBytes(string sourcePath, byte[] bytes)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		fileSystem.WriteAllBytes(path, bytes);
	}

	public static ValueTask WriteAllBytesAsync(string sourcePath, byte[] bytes, CancellationToken cancellationToken = default)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.WriteAllBytesAsync(path, bytes, cancellationToken);
	}

	public static string ReadAllText(string sourcePath)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.ReadAllText(path);
	}

	public static ValueTask<string> ReadAllTextAsync(string sourcePath, CancellationToken cancellationToken = default)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.ReadAllTextAsync(path, cancellationToken);
	}

	public static void WriteAllText(string sourcePath, string contents)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		fileSystem.WriteAllText(path, contents);
	}

	public static ValueTask WriteAllTextAsync(string sourcePath, string contents, CancellationToken cancellationToken = default)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.WriteAllTextAsync(path, contents, cancellationToken);
	}

	public static long GetFileLength(string sourcePath)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.GetFileLength(path);
	}

	public static ValueTask<long> GetFileLengthAsync(string sourcePath, CancellationToken cancellationToken = default)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.GetFileLengthAsync(path, cancellationToken);
	}

	public static void CreateDirectory(string sourcePath)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		fileSystem.CreateDirectory(path);
	}

	public static ValueTask CreateDirectoryAsync(string sourcePath, CancellationToken cancellationToken = default)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.CreateDirectoryAsync(path, cancellationToken);
	}

	public static void DeleteFile(string sourcePath)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		fileSystem.DeleteFile(path);
	}

	public static ValueTask DeleteFileAsync(string sourcePath, CancellationToken cancellationToken = default)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.DeleteFileAsync(path, cancellationToken);
	}

	public static void DeleteDirectory(string sourcePath, bool recursive = false)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		fileSystem.DeleteDirectory(path, recursive);
	}

	public static ValueTask DeleteDirectoryAsync(string sourcePath, bool recursive = false, CancellationToken cancellationToken = default)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.DeleteDirectoryAsync(path, recursive, cancellationToken);
	}

	public static void CopyFile(string sourcePath, string destinationPath, bool overwrite = false)
	{
		var (fileSystem, source, destination) = ResolvePair(sourcePath, destinationPath);
		fileSystem.CopyFile(source, destination, overwrite);
	}

	public static ValueTask CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default)
	{
		var (fileSystem, source, destination) = ResolvePair(sourcePath, destinationPath);
		return fileSystem.CopyFileAsync(source, destination, overwrite, cancellationToken);
	}

	public static void MoveFile(string sourcePath, string destinationPath, bool overwrite = false)
	{
		var (fileSystem, source, destination) = ResolvePair(sourcePath, destinationPath);
		fileSystem.MoveFile(source, destination, overwrite);
	}

	public static ValueTask MoveFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default)
	{
		var (fileSystem, source, destination) = ResolvePair(sourcePath, destinationPath);
		return fileSystem.MoveFileAsync(source, destination, overwrite, cancellationToken);
	}

	public static IEnumerable<FileSystemEntry> EnumerateEntries(string sourcePath)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.EnumerateEntries(path);
	}

	public static IEnumerable<string> EnumerateFiles(string sourcePath, string pattern = "*",
		SearchOption searchOption = SearchOption.TopDirectoryOnly)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.EnumerateFiles(path, pattern, searchOption);
	}

	public static IEnumerable<string> EnumerateDirectories(string sourcePath, string pattern = "*",
		SearchOption searchOption = SearchOption.TopDirectoryOnly)
	{
		var (fileSystem, path) = Resolve(sourcePath);
		return fileSystem.EnumerateDirectories(path, pattern, searchOption);
	}

	private static (FileSystem FileSystem, string Source, string Destination) ResolvePair(string sourcePath, string destinationPath)
	{
		var source = Resolve(sourcePath);
		var destination = Resolve(destinationPath);

		if (!ReferenceEquals(source.FileSystem, destination.FileSystem))
			throw new ArgumentException($"'{sourcePath}' and '{destinationPath}' are not mounted on the same file system", nameof(destinationPath));

		return (source.FileSystem, source.Path, destination.Path);
	}

	private static void ValidateHead(string head)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(head);

		if (head.Any(character => char.IsWhiteSpace(character) || character is '/' or '\\' or ':'))
			throw new ArgumentException($"head '{head}' must not contain whitespace, '/', '\\' or ':'", nameof(head));
	}
}
