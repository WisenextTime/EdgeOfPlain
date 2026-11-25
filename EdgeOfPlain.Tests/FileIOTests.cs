using System;
using System.IO;
using System.Linq;
using EdgeOfPlain.Core.Framework.IO;
using Xunit;
using Zio;
using Zio.FileSystems;

namespace EdgeOfPlain.Tests;

public sealed class FileIOTests
{
	[Fact]
	public void InitializeMountsTheDataAndModDirectories()
	{
		using var scope = new GameIoScope();

		Assert.True(FileIO.IsInitialized);
		Assert.True(scope.Root.DirectoryExists(FileIO.Data));
		Assert.True(scope.Root.DirectoryExists(FileIO.Mods));
		Assert.True(Directory.Exists(scope.Data.Path));
		Assert.True(Directory.Exists(scope.Mods.Path));
	}

	[Fact]
	public void WritesUnderTheDataMountLandOnTheHost()
	{
		using var scope = new GameIoScope();
		var path = FileIO.Data / "logs" / "latest.log";

		scope.Root.AddText(path, "hello");

		Assert.Equal("hello", File.ReadAllText(Path.Combine(scope.Data.Path, "logs", "latest.log")));
	}

	[Fact]
	public void RootThrowsBeforeInitialize()
	{
		FileIO.Reset();

		Assert.False(FileIO.IsInitialized);
		var error = Assert.Throws<InvalidOperationException>(() => FileIO.Root);
		Assert.Contains("has not been initialized", error.Message);
	}

	[Fact]
	public void InitializeRejectsASecondCall()
	{
		using var scope = new GameIoScope();

		var error = Assert.Throws<InvalidOperationException>(() => FileIO.Initialize(scope.Data.Path, scope.Mods.Path));
		Assert.Contains("already been initialized", error.Message);
	}

	[Fact]
	public void MountedFileSystemsAreVisibleThroughTheRoot()
	{
		using var scope = new GameIoScope();
		var path = FileIO.Assets / "probe" / "data.txt";

		scope.Root.AddText(path, "value");

		Assert.True(scope.Root.FileExists(path));
		Assert.Equal("value", TestPath.ReadAllText(scope.Root, path));
	}

	[Fact]
	public void UnmountReturnsTheMountedFileSystem()
	{
		using var scope = new GameIoScope();
		var path = FileIO.Assets / "probe" / "data.txt";
		scope.Root.AddText(path, "value");

		var unmounted = FileIO.Unmount(FileIO.Assets);

		Assert.Same(scope.Assets, unmounted);
		Assert.False(scope.Root.FileExists(path));
	}

	[Fact]
	public void TryGetMountedReportsExactMountNames()
	{
		using var scope = new GameIoScope();

		Assert.True(FileIO.TryGetMounted(FileIO.Assets, out var assets));
		Assert.Same(scope.Assets, assets);
		Assert.False(FileIO.TryGetMounted(new UPath("/assets/probe"), out _));
	}

	[Fact]
	public void TryGetPhysicalPathResolvesAMountedHostDirectory()
	{
		using var scope = new GameIoScope();
		var path = FileIO.Data / "cache" / "entry.bin";
		scope.Root.AddBytes(path.FullName, [1, 2, 3]);

		Assert.True(scope.Root.TryGetPhysicalPath(path, out var physical));

		Assert.Equal(Path.Combine(scope.Data.Path, "cache", "entry.bin"), physical);
		Assert.True(File.Exists(physical));
	}

	[Fact]
	public void TryGetPhysicalPathFailsForMemoryMounts()
	{
		using var scope = new GameIoScope();
		var path = FileIO.Assets / "probe" / "data.txt";
		scope.Root.AddText(path, "value");

		Assert.False(scope.Root.TryGetPhysicalPath(path, out var physical));
		Assert.Null(physical);
	}

	[Fact]
	public void OpenReadSharedAllowsConcurrentReaders()
	{
		using var scope = new GameIoScope();
		var path = FileIO.Assets / "probe" / "data.txt";
		scope.Root.AddText(path, "value");

		using var first = scope.Root.OpenReadShared(path);
		using var second = scope.Root.OpenReadShared(path);

		Assert.Equal("value", new StreamReader(first).ReadToEnd());
		Assert.Equal("value", new StreamReader(second).ReadToEnd());
	}

	[Fact]
	public void CreateParentDirectoryCreatesTheMissingChain()
	{
		using var scope = new GameIoScope();
		var path = new UPath("/assets/a/b/c/data.txt");

		scope.Root.CreateParentDirectory(path);

		Assert.True(scope.Assets.DirectoryExists(new UPath("/a/b/c")));
	}

	[Fact]
	public void ResetDisposesTheMountTable()
	{
		var scope = new GameIoScope();
		var assets = scope.Assets;
		scope.Root.AddText(FileIO.Assets / "probe" / "data.txt", "value");

		FileIO.Reset();

		Assert.False(FileIO.IsInitialized);
		Assert.Throws<ObjectDisposedException>(() => assets.FileExists(FileIO.Assets / "probe" / "data.txt"));
		scope.Data.Dispose();
		scope.Mods.Dispose();
	}

	[Fact]
	public void MountingTheSamePathTwiceThrows()
	{
		using var scope = new GameIoScope();

		var error = Assert.Throws<ArgumentException>(() => FileIO.Mount(FileIO.Assets, new MemoryFileSystem()));
		Assert.Contains("already a mount", error.Message);
	}

	[Fact]
	public void EnumerationMergesEveryMount()
	{
		using var scope = new GameIoScope();
		scope.Assets.AddText("/probe/a.txt", "a");
		scope.Root.AddText(FileIO.Data / "b.txt", "b");

		var roots = scope.Root.EnumerateDirectories(UPath.Root, "*", SearchOption.TopDirectoryOnly)
			.Select(path => path.FullName)
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToArray();

		Assert.Equal(["/assets", "/data", "/mod"], roots);
	}
}