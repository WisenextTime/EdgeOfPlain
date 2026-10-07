using System;
using System.Collections.Generic;
using System.IO;
using FileSystemName = System.IO.Enumeration.FileSystemName;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EdgeOfPlain.Core.Framework.IO;

public readonly record struct FileSystemEntry(string Path, bool IsDirectory);

public abstract class FileSystem : IDisposable, IAsyncDisposable
{
	private static readonly Encoding _utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	public bool IsDisposed { get; private set; }

	public virtual bool IsReadOnly => false;

	public virtual bool IsCaseSensitive => true;

	public StringComparer PathComparer => IsCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

	public bool FileExists(string path)
	{
		ThrowIfDisposed();
		return FileExistsCore(NormalizePath(path));
	}

	protected abstract bool FileExistsCore(string path);

	public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		return FileExistsCoreAsync(NormalizePath(path), cancellationToken);
	}

	protected virtual ValueTask<bool> FileExistsCoreAsync(string path, CancellationToken cancellationToken) 
		=> new(FileExistsCore(path));

	public bool DirectoryExists(string path)
	{
		ThrowIfDisposed();
		return DirectoryExistsCore(NormalizePath(path));
	}

	protected abstract bool DirectoryExistsCore(string path);

	public ValueTask<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		return DirectoryExistsCoreAsync(NormalizePath(path), cancellationToken);
	}

	protected virtual ValueTask<bool> DirectoryExistsCoreAsync(string path, CancellationToken cancellationToken)
		=> new(DirectoryExistsCore(path));

	public Stream OpenRead(string path)
	{
		ThrowIfDisposed();
		return OpenReadCore(NormalizePath(path));
	}

	protected abstract Stream OpenReadCore(string path);

	public ValueTask<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		return OpenReadCoreAsync(NormalizePath(path), cancellationToken);
	}

	protected virtual ValueTask<Stream> OpenReadCoreAsync(string path, CancellationToken cancellationToken)
		=> new(OpenReadCore(path));

	public Stream OpenWrite(string path)
	{
		ThrowIfDisposed();
		return OpenWriteCore(NormalizePath(path));
	}

	protected virtual Stream OpenWriteCore(string path) => throw Unsupported(nameof(OpenWrite), path);

	public ValueTask<Stream> OpenWriteAsync(string path, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		return OpenWriteCoreAsync(NormalizePath(path), cancellationToken);
	}

	protected virtual ValueTask<Stream> OpenWriteCoreAsync(string path, CancellationToken cancellationToken)
		=> new(OpenWriteCore(path));

	public void DeleteFile(string path)
	{
		ThrowIfDisposed();
		DeleteFileCore(NormalizePath(path));
	}

	protected virtual void DeleteFileCore(string path) => throw Unsupported(nameof(DeleteFile), path);

	public ValueTask DeleteFileAsync(string path, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		return DeleteFileCoreAsync(NormalizePath(path), cancellationToken);
	}

	protected virtual ValueTask DeleteFileCoreAsync(string path, CancellationToken cancellationToken)
	{
		DeleteFileCore(path);
		return ValueTask.CompletedTask;
	}

	public void CreateDirectory(string path)
	{
		ThrowIfDisposed();
		CreateDirectoryCore(NormalizePath(path));
	}

	protected virtual void CreateDirectoryCore(string path) => throw Unsupported(nameof(CreateDirectory), path);

	public ValueTask CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		return CreateDirectoryCoreAsync(NormalizePath(path), cancellationToken);
	}

	protected virtual ValueTask CreateDirectoryCoreAsync(string path, CancellationToken cancellationToken)
	{
		CreateDirectoryCore(path);
		return ValueTask.CompletedTask;
	}

	public void DeleteDirectory(string path, bool recursive = false)
	{
		ThrowIfDisposed();
		DeleteDirectoryCore(NormalizePath(path), recursive);
	}

	protected virtual void DeleteDirectoryCore(string path, bool recursive) => throw Unsupported(nameof(DeleteDirectory), path);

	public ValueTask DeleteDirectoryAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		return DeleteDirectoryCoreAsync(NormalizePath(path), recursive, cancellationToken);
	}

	protected virtual ValueTask DeleteDirectoryCoreAsync(string path, bool recursive, CancellationToken cancellationToken)
	{
		DeleteDirectoryCore(path, recursive);
		return ValueTask.CompletedTask;
	}

	// Enumeration stays synchronous on purpose: System.IO exposes no asynchronous directory walking either.
	public IEnumerable<FileSystemEntry> EnumerateEntries(string path = "")
	{
		ThrowIfDisposed();
		return EnumerateEntriesCore(NormalizePath(path));
	}

	protected abstract IEnumerable<FileSystemEntry> EnumerateEntriesCore(string path);

	public byte[] ReadAllBytes(string path)
	{
		using var stream = OpenRead(path);
		using var buffer = new MemoryStream(stream is { CanSeek: true, Length: <= int.MaxValue } ? (int)stream.Length : 0);
		stream.CopyTo(buffer);
		return buffer.ToArray();
	}

	public async ValueTask<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
	{
		await using var stream = await OpenReadAsync(path, cancellationToken).ConfigureAwait(false);
		using var buffer = new MemoryStream(stream is { CanSeek: true, Length: <= int.MaxValue } ? (int)stream.Length : 0);
		await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
		return buffer.ToArray();
	}

	public string ReadAllText(string path)
	{
		using var stream = OpenRead(path);
		using var reader = new StreamReader(stream, _utf8, detectEncodingFromByteOrderMarks: true);
		return reader.ReadToEnd();
	}

	public async ValueTask<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
	{
		await using var stream = await OpenReadAsync(path, cancellationToken).ConfigureAwait(false);
		using var reader = new StreamReader(stream, _utf8, detectEncodingFromByteOrderMarks: true);
		return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
	}

	public void WriteAllBytes(string path, byte[] bytes)
	{
		ArgumentNullException.ThrowIfNull(bytes);
		using var stream = OpenWrite(path);
		stream.Write(bytes, 0, bytes.Length);
	}

	public async ValueTask WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(bytes);
		await using var stream = await OpenWriteAsync(path, cancellationToken).ConfigureAwait(false);
		await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
	}

	public void WriteAllText(string path, string contents)
	{
		ArgumentNullException.ThrowIfNull(contents);
		using var stream = OpenWrite(path);
		using var writer = new StreamWriter(stream, _utf8);
		writer.Write(contents);
	}

	public async ValueTask WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(contents);
		await using var stream = await OpenWriteAsync(path, cancellationToken).ConfigureAwait(false);
		await using var writer = new StreamWriter(stream, _utf8);
		await writer.WriteAsync(contents.AsMemory(), cancellationToken).ConfigureAwait(false);
		await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
	}

	public void CopyFile(string source, string destination, bool overwrite = false)
	{
		if (!overwrite && FileExists(destination)) throw new IOException($"file already exists: '{NormalizePath(destination)}'");

		using var input = OpenRead(source);
		using var output = OpenWrite(destination);
		input.CopyTo(output);
	}

	public async ValueTask CopyFileAsync(string source, string destination, bool overwrite = false,
		CancellationToken cancellationToken = default)
	{
		if (!overwrite && await FileExistsAsync(destination, cancellationToken))
			throw new IOException($"file already exists: '{NormalizePath(destination)}'");

		await using var input = await OpenReadAsync(source, cancellationToken).ConfigureAwait(false);
		await using var output = await OpenWriteAsync(destination, cancellationToken).ConfigureAwait(false);
		await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
	}

	public void MoveFile(string source, string destination, bool overwrite = false)
	{
		CopyFile(source, destination, overwrite);
		DeleteFile(source);
	}

	public async ValueTask MoveFileAsync(string source, string destination, bool overwrite = false,
		CancellationToken cancellationToken = default)
	{
		await CopyFileAsync(source, destination, overwrite, cancellationToken).ConfigureAwait(false);
		await DeleteFileAsync(source, cancellationToken).ConfigureAwait(false);
	}

	public long GetFileLength(string path)
	{
		using var stream = OpenRead(path);
		return stream.Length;
	}

	public async ValueTask<long> GetFileLengthAsync(string path, CancellationToken cancellationToken = default)
	{
		await using var stream = await OpenReadAsync(path, cancellationToken).ConfigureAwait(false);
		return stream.Length;
	}

	public IEnumerable<string> EnumerateFiles(string path = "", string pattern = "*",
		SearchOption searchOption = SearchOption.TopDirectoryOnly)
		=> Enumerate(path, pattern, searchOption, wantDirectories: false);

	public IEnumerable<string> EnumerateDirectories(string path = "", string pattern = "*",
		SearchOption searchOption = SearchOption.TopDirectoryOnly)
		=> Enumerate(path, pattern, searchOption, wantDirectories: true);

	public void Dispose()
	{
		if (IsDisposed) return;

		IsDisposed = true;
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	protected virtual void Dispose(bool disposing) { }

	protected static string NormalizePath(string path)
	{
		ArgumentNullException.ThrowIfNull(path);
		if (path.Length == 0) return string.Empty;

		var segments = new List<string>(4);
		foreach (var segment in path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
		{
			switch (segment)
			{
				case ".":
					continue;
				case "..":
					if (segments.Count == 0)
						throw new ArgumentException($"path escapes the file system root: '{path}'", nameof(path));
					segments.RemoveAt(segments.Count - 1);
					continue;
				default:
					if (segment.Contains(':'))
						throw new ArgumentException($"invalid path segment '{segment}' in '{path}'", nameof(path));
					segments.Add(segment);
					continue;
			}
		}

		return string.Join('/', segments);
	}

	private IEnumerable<string> Enumerate(string path, string pattern, SearchOption searchOption, bool wantDirectories)
	{
		ArgumentNullException.ThrowIfNull(pattern);

		foreach (var entry in EnumerateEntries(path))
		{
			if (entry.IsDirectory == wantDirectories &&
			    FileSystemName.MatchesSimpleExpression(pattern, GetName(entry.Path), ignoreCase: !IsCaseSensitive))
				yield return entry.Path;

			if (!entry.IsDirectory || searchOption != SearchOption.AllDirectories) continue;

			foreach (var nested in Enumerate(entry.Path, pattern, searchOption, wantDirectories)) yield return nested;
		}
	}

	private static ReadOnlySpan<char> GetName(string path)
	{
		var index = path.LastIndexOf('/');
		return index < 0 ? path.AsSpan() : path.AsSpan(index + 1);
	}

	private NotSupportedException Unsupported(string operation, string path)
		=> new($"{GetType().Name} does not support {operation}('{path}')");

	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);
}