using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EdgeOfPlain.Core.Framework.IO;

public sealed class PhysicalFileSystem(string root) : FileSystem
{
	public string Root { get; } = Path.GetFullPath(root);

	public override bool IsCaseSensitive => !OperatingSystem.IsWindows();

	public override bool TryGetPhysicalPath(string path, out string physicalPath)
	{
		physicalPath = Resolve(NormalizePath(path));
		return true;
	}

	protected override bool FileExistsCore(string path) => File.Exists(Resolve(path));

	protected override bool DirectoryExistsCore(string path) => Directory.Exists(Resolve(path));

	protected override Stream OpenReadCore(string path)
		=> new FileStream(Resolve(path), FileMode.Open, FileAccess.Read, FileShare.Read);

	protected override ValueTask<Stream> OpenReadCoreAsync(string path, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return new ValueTask<Stream>(new FileStream(
			Resolve(path), FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
			FileOptions.Asynchronous | FileOptions.SequentialScan));
	}

	protected override Stream OpenWriteCore(string path)
	{
		var resolved = Resolve(path);
		Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);
		return new FileStream(resolved, FileMode.Create, FileAccess.Write, FileShare.None);
	}

	protected override ValueTask<Stream> OpenWriteCoreAsync(string path, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var resolved = Resolve(path);
		Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);
		return new ValueTask<Stream>(new FileStream(
			resolved, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous));
	}

	protected override void DeleteFileCore(string path) => File.Delete(Resolve(path));

	protected override void CreateDirectoryCore(string path) => Directory.CreateDirectory(Resolve(path));

	protected override void DeleteDirectoryCore(string path, bool recursive) => Directory.Delete(Resolve(path), recursive);

	protected override IEnumerable<FileSystemEntry> EnumerateEntriesCore(string path)
	{
		return new DirectoryInfo(Resolve(path)).EnumerateFileSystemInfos().Select(info
			=> new FileSystemEntry(Path.GetRelativePath(Root, info.FullName).Replace('\\', '/'), info is DirectoryInfo));
	}

	private string Resolve(string path)
		=> path.Length == 0 ? Root : Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
}
