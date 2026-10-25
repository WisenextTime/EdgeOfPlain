using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EdgeOfPlain.Core.Framework.IO;
using Xunit;

namespace EdgeOfPlain.Tests;

internal sealed class HeadScope : IDisposable
{
	public string Head { get; } = UniqueHead("eop-io-");

	public MemoryFileSystem FileSystem { get; }

	public HeadScope(MemoryFileSystem? fileSystem = null)
	{
		FileSystem = fileSystem ?? new MemoryFileSystem();
		FileIO.RegisterFileSystem(Head, FileSystem);
	}

	public string this[string path] => $"{Head}{FileIO.SchemeSeparator}{path}";

	public void Dispose() => FileIO.UnregisterFileSystem(Head);

	public static string UniqueHead(string prefix) => prefix + Guid.NewGuid().ToString("N");
}

public sealed class FileIOTests
{
	[Fact]
	public void RegisterAndResolve_ExposesMountedFileSystem()
	{
		using var scope = new HeadScope(new MemoryFileSystem().Add("a/b.txt", "hello"));

		Assert.True(FileIO.IsRegistered(scope.Head));
		Assert.Contains(scope.Head, FileIO.RegisteredHeads);
		Assert.True(FileIO.TryResolve(scope["a/b.txt"], out var location));
		Assert.Same(scope.FileSystem, location.FileSystem);
		Assert.Equal("a/b.txt", location.Path);
		Assert.Equal("hello", FileIO.ReadAllText(scope["a/b.txt"]));
	}

	[Fact]
	public void Register_RejectsDuplicateAndInvalidHeads()
	{
		using var scope = new HeadScope();

		Assert.Throws<ArgumentException>(() => FileIO.RegisterFileSystem(scope.Head, new MemoryFileSystem()));
		Assert.Throws<ArgumentNullException>(() => FileIO.RegisterFileSystem(null!, new MemoryFileSystem()));
		Assert.Throws<ArgumentNullException>(() => FileIO.RegisterFileSystem(HeadScope.UniqueHead("eop-null-"), (FileSystem)null!));
		Assert.Throws<ArgumentNullException>(()
			=> FileIO.RegisterFileSystem(HeadScope.UniqueHead("eop-fn-"), (Func<FileSystem>)null!));

		foreach (var head in new[] { " ", "bad head", "bad/head", "bad\\head", "bad:head" })
			Assert.Throws<ArgumentException>(() => FileIO.RegisterFileSystem(head, new MemoryFileSystem()));
	}

	[Fact]
	public void TryRegisterFileSystem_ReportsConflictsWithoutThrowing()
	{
		var head = HeadScope.UniqueHead("eop-try-");
		var first = new MemoryFileSystem().Add("a.txt", "first");
		var second = new MemoryFileSystem().Add("a.txt", "second");

		try
		{
			Assert.True(FileIO.TryRegisterFileSystem(head, first));
			Assert.Equal("first", FileIO.ReadAllText($"{head}{FileIO.SchemeSeparator}a.txt"));

			Assert.False(FileIO.TryRegisterFileSystem(head, second));
			Assert.False(second.IsDisposed);
			Assert.Equal("first", FileIO.ReadAllText($"{head}{FileIO.SchemeSeparator}a.txt"));
		}
		finally
		{
			FileIO.UnregisterFileSystem(head);
		}
	}

	[Fact]
	public void TryRegisterFileSystem_DoesNotInvokeFactoryOnConflict()
	{
		var head = HeadScope.UniqueHead("eop-try-factory-");
		var created = 0;

		try
		{
			Assert.True(FileIO.TryRegisterFileSystem(head, () =>
			{
				created++;
				return new MemoryFileSystem();
			}));
			Assert.Equal(0, created);

			Assert.False(FileIO.TryRegisterFileSystem(head, () =>
			{
				created++;
				return new MemoryFileSystem();
			}));
			Assert.Equal(0, created);
		}
		finally
		{
			FileIO.UnregisterFileSystem(head);
		}
	}

	[Fact]
	public void LazyFactory_IsInvokedOnlyOnce()
	{
		var head = HeadScope.UniqueHead("eop-lazy-");
		var created = 0;
		FileIO.RegisterFileSystem(head, () =>
		{
			created++;
			return new MemoryFileSystem().Add("a.txt", "lazy");
		});

		try
		{
			Assert.Equal(0, created);
			Assert.Equal("lazy", FileIO.ReadAllText($"{head}{FileIO.SchemeSeparator}a.txt"));
			Assert.Equal(1, created);
			Assert.True(FileIO.FileExists($"{head}{FileIO.SchemeSeparator}a.txt"));
			Assert.True(FileIO.DirectoryExists($"{head}{FileIO.SchemeSeparator}"));
			Assert.Equal(1, created);
		}
		finally
		{
			FileIO.UnregisterFileSystem(head);
		}
	}

	[Fact]
	public void Unregister_RemovesHeadAndReportsWhetherItExisted()
	{
		using var scope = new HeadScope();

		Assert.True(FileIO.UnregisterFileSystem(scope.Head));
		Assert.False(FileIO.IsRegistered(scope.Head));
		Assert.False(FileIO.UnregisterFileSystem(scope.Head));
		Assert.Throws<ArgumentNullException>(() => FileIO.UnregisterFileSystem(null!));
		Assert.Throws<ArgumentNullException>(() => FileIO.IsRegistered(null!));
	}

	[Fact]
	public void Clear_RemovesEveryHeadAndDisposesCreatedMounts()
	{
		var created = new MemoryFileSystem();
		var disposableHead = HeadScope.UniqueHead("eop-clear-a-");
		var lazyHead = HeadScope.UniqueHead("eop-clear-b-");
		FileIO.RegisterFileSystem(disposableHead, created);
		FileIO.RegisterFileSystem(lazyHead, () => new MemoryFileSystem());

		Assert.True(FileIO.TryResolve($"{disposableHead}{FileIO.SchemeSeparator}x.txt", out _));
		Assert.True(FileIO.TryResolve($"{lazyHead}{FileIO.SchemeSeparator}x.txt", out _));

		FileIO.Clear();

		Assert.False(FileIO.IsRegistered(disposableHead));
		Assert.False(FileIO.IsRegistered(lazyHead));
		Assert.True(created.IsDisposed);
		Assert.Empty(FileIO.RegisteredHeads);
		Assert.False(FileIO.UnregisterFileSystem(disposableHead));
	}

	[Fact]
	public void TryResolve_RejectsMalformedOrUnknownPaths()
	{
		using var scope = new HeadScope();

		Assert.False(FileIO.TryResolve(null, out _));
		Assert.False(FileIO.TryResolve(string.Empty, out _));
		Assert.False(FileIO.TryResolve("   ", out _));
		Assert.False(FileIO.TryResolve("plain.txt", out _));
		Assert.False(FileIO.TryResolve("://a.txt", out _));
		Assert.False(FileIO.TryResolve("eop-unknown-" + Guid.NewGuid().ToString("N") + "://a.txt", out _));
		Assert.True(FileIO.TryResolve(scope[string.Empty], out var root));
		Assert.Equal(string.Empty, root.Path);
	}

	[Fact]
	public void Resolve_ThrowsDescriptiveErrors()
	{
		Assert.Throws<ArgumentNullException>(() => FileIO.Resolve(null!));
		Assert.Throws<ArgumentException>(() => FileIO.Resolve(" "));

		var noHead = Assert.Throws<ArgumentException>(() => FileIO.Resolve("plain.txt"));
		Assert.Contains("has no", noHead.Message);

		var emptyHead = Assert.Throws<ArgumentException>(() => FileIO.Resolve("://a.txt"));
		Assert.Contains("empty head", emptyHead.Message);

		var unknownHead = Assert.Throws<ArgumentException>(() => FileIO.Resolve("eop-unknown://a.txt"));
		Assert.Contains("no file system is registered", unknownHead.Message);
	}

	[Fact]
	public void ReadWriteText_RoundTripsThroughMount()
	{
		using var scope = new HeadScope();

		FileIO.WriteAllText(scope["dir/a.txt"], "héllo");

		Assert.True(FileIO.FileExists(scope["dir/a.txt"]));
		Assert.True(FileIO.DirectoryExists(scope["dir"]));
		Assert.True(FileIO.Exists(scope["dir/a.txt"]));
		Assert.True(FileIO.Exists(scope["dir"]));
		Assert.Equal("héllo", FileIO.ReadAllText(scope["dir/a.txt"]));
		Assert.Equal(Encoding.UTF8.GetByteCount("héllo"), FileIO.GetFileLength(scope["dir/a.txt"]));
		Assert.False(FileIO.FileExists(scope["dir/missing.txt"]));

		FileIO.WriteAllText(scope["dir/a.txt"], "replaced");
		Assert.Equal("replaced", FileIO.ReadAllText(scope["dir/a.txt"]));
	}

	[Fact]
	public void ReadWriteBytes_RoundTripsThroughMount()
	{
		using var scope = new HeadScope();
		var bytes = new byte[] { 1, 2, 3, 255 };

		FileIO.WriteAllBytes(scope["b.bin"], bytes);

		Assert.Equal(bytes, FileIO.ReadAllBytes(scope["b.bin"]));
		Assert.Equal(4L, FileIO.GetFileLength(scope["b.bin"]));
	}

	[Fact]
	public async Task AsyncVariants_RoundTripContentAndDirectories()
	{
		using var scope = new HeadScope();

		var cancellationToken = TestContext.Current.CancellationToken;

		await FileIO.WriteAllTextAsync(scope["a.txt"], "async", cancellationToken);
		Assert.Equal("async", await FileIO.ReadAllTextAsync(scope["a.txt"], cancellationToken));

		await FileIO.WriteAllBytesAsync(scope["b.bin"], [1, 2, 3], cancellationToken);
		Assert.Equal(new byte[] { 1, 2, 3 }, await FileIO.ReadAllBytesAsync(scope["b.bin"], cancellationToken));
		Assert.Equal(3L, await FileIO.GetFileLengthAsync(scope["b.bin"], cancellationToken));

		await FileIO.CreateDirectoryAsync(scope["async-dir"], cancellationToken);
		Assert.True(FileIO.DirectoryExists(scope["async-dir"]));

		await FileIO.CopyFileAsync(scope["a.txt"], scope["copy.txt"], cancellationToken: cancellationToken);
		Assert.Equal("async", FileIO.ReadAllText(scope["copy.txt"]));

		await FileIO.MoveFileAsync(scope["copy.txt"], scope["moved.txt"], cancellationToken: cancellationToken);
		Assert.False(FileIO.FileExists(scope["copy.txt"]));
		Assert.Equal("async", FileIO.ReadAllText(scope["moved.txt"]));

		await FileIO.DeleteFileAsync(scope["moved.txt"], cancellationToken);
		Assert.False(FileIO.FileExists(scope["moved.txt"]));

		await FileIO.DeleteDirectoryAsync(scope["async-dir"], cancellationToken: cancellationToken);
		Assert.False(FileIO.DirectoryExists(scope["async-dir"]));
	}

	[Fact]
	public async Task OpenReadAndOpenWrite_ReturnUsableStreams()
	{
		using var scope = new HeadScope();

		using (var write = await FileIO.OpenWriteAsync(scope["a.txt"], TestContext.Current.CancellationToken))
			write.Write("stream"u8);

		Assert.Equal("stream", await FileIO.ReadAllTextAsync(scope["a.txt"], TestContext.Current.CancellationToken));

		using (var read = await FileIO.OpenReadAsync(scope["a.txt"], TestContext.Current.CancellationToken))
		using (var reader = new StreamReader(read))
			Assert.Equal("stream", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));

		var cancellationToken = TestContext.Current.CancellationToken;

		using (var write = await FileIO.OpenWriteAsync(scope["b.txt"], cancellationToken))
			await write.WriteAsync("async"u8.ToArray(), cancellationToken);

		Assert.Equal("async", FileIO.ReadAllText(scope["b.txt"]));

		using (var read = await FileIO.OpenReadAsync(scope["b.txt"], cancellationToken))
		using (var reader = new StreamReader(read))
			Assert.Equal("async", reader.ReadToEnd());
	}

	[Fact]
	public void CopyMoveDelete_WorkThroughMount()
	{
		using var scope = new HeadScope();

		FileIO.WriteAllText(scope["a.txt"], "a");
		FileIO.CopyFile(scope["a.txt"], scope["copy.txt"]);
		Assert.Equal("a", FileIO.ReadAllText(scope["copy.txt"]));
		Assert.Throws<IOException>(() => FileIO.CopyFile(scope["a.txt"], scope["copy.txt"]));

		FileIO.CopyFile(scope["a.txt"], scope["copy.txt"], true);
		Assert.Equal("a", FileIO.ReadAllText(scope["copy.txt"]));

		FileIO.MoveFile(scope["a.txt"], scope["moved.txt"]);
		Assert.False(FileIO.FileExists(scope["a.txt"]));
		Assert.Equal("a", FileIO.ReadAllText(scope["moved.txt"]));

		FileIO.DeleteFile(scope["moved.txt"]);
		Assert.False(FileIO.FileExists(scope["moved.txt"]));
		Assert.Throws<FileNotFoundException>(() => FileIO.DeleteFile(scope["moved.txt"]));
	}

	[Fact]
	public void CopyMoveAcrossHeads_Throws()
	{
		using var first = new HeadScope(new MemoryFileSystem().Add("a.txt", "a"));
		using var second = new HeadScope();

		Assert.Throws<ArgumentException>(() => FileIO.CopyFile(first["a.txt"], second["b.txt"]));
		Assert.Throws<ArgumentException>(() => FileIO.MoveFile(first["a.txt"], second["b.txt"]));
	}

	[Fact]
	public void CreateAndDeleteDirectory_FollowRecursiveFlag()
	{
		using var scope = new HeadScope();

		FileIO.CreateDirectory(scope["new/deep"]);
		Assert.True(FileIO.DirectoryExists(scope["new"]));
		Assert.True(FileIO.DirectoryExists(scope["new/deep"]));

		FileIO.WriteAllText(scope["new/deep/a.txt"], "x");
		Assert.Throws<IOException>(() => FileIO.DeleteDirectory(scope["new/deep"]));

		FileIO.DeleteDirectory(scope["new/deep"], true);
		Assert.False(FileIO.DirectoryExists(scope["new/deep"]));
		Assert.False(FileIO.FileExists(scope["new/deep/a.txt"]));
		Assert.True(FileIO.DirectoryExists(scope["new"]));
	}

	[Fact]
	public void EnumerateEntriesAndFiles_AreHeadPrefixed()
	{
		using var scope = new HeadScope(new MemoryFileSystem()
			.Add("dir/a.txt", "a")
			.Add("dir/deep/b.txt", "b")
			.Add("root.txt", "r"));

		var root = FileIO.EnumerateEntries(scope[string.Empty]).ToArray();

		Assert.Contains(root, entry => entry.Path == scope["root.txt"] && !entry.IsDirectory);
		Assert.Contains(root, entry => entry.Path == scope["dir"] && entry.IsDirectory);
		Assert.Equal(new[] { scope["root.txt"] }, FileIO.EnumerateFiles(scope[string.Empty]).ToArray());
		Assert.Equal(
			new[] { scope["dir/a.txt"], scope["dir/deep/b.txt"] },
			FileIO.EnumerateFiles(scope["dir"], "*", SearchOption.AllDirectories).OrderBy(file => file).ToArray());
	}

	[Fact]
	public void EnumerateDirectories_AreHeadPrefixed()
	{
		using var scope = new HeadScope(new MemoryFileSystem()
			.Add("dir/a.txt", "a")
			.Add("dir/deep/b.txt", "b")
			.Add("other/c.txt", "c"));

		Assert.Equal(new[] { scope["dir"], scope["other"] }, FileIO.EnumerateDirectories(scope[string.Empty]).ToArray());
		Assert.Equal(
			new[] { scope["dir/deep"] },
			FileIO.EnumerateDirectories(scope["dir"]).ToArray());
		Assert.Empty(FileIO.EnumerateDirectories(scope[string.Empty], "nomatch*"));
	}
}
