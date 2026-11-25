using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using EdgeOfPlain.Core.Framework.IO;
using Xunit;
using Zio;
using Zio.FileSystems;

namespace EdgeOfPlain.Tests;

public sealed class ZipFileSystemTests
{
	[Fact]
	public void RawZioZipDoesNotReportImplicitFolders()
	{
		var path = CreateArchive(("nested/b.txt", "b"));
		using var zip = new ZipArchiveFileSystem(path, ZipArchiveMode.Read, leaveOpen: false, isCaseSensitive: false);

		Assert.False(zip.DirectoryExists(new UPath("/nested")));
	}

	private static string CreateArchive(params (string Name, string Content)[] entries)
	{
		var path = Path.Combine(AppContext.BaseDirectory, "test-temp", Guid.NewGuid().ToString("N") + ".zip");
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);

		using (var stream = File.Create(path))
		using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
		{
			foreach (var (name, content) in entries)
			{
				var entry = archive.CreateEntry(name);
				using var writer = new StreamWriter(entry.Open());
				writer.Write(content);
			}
		}

		return path;
	}

	private static ZipFileSystem Open(string path)
		=> new(path, isCaseSensitive: false);

	[Fact]
	public void ReadsEntriesAndFolders()
	{
		var path = CreateArchive(("a.txt", "a"), ("nested/b.txt", "b"));
		using var zip = Open(path);

		Assert.True(zip.FileExists(new UPath("/a.txt")));
		Assert.True(zip.FileExists(new UPath("/NESTED/B.TXT")));
		Assert.True(zip.DirectoryExists(new UPath("/nested")));
		Assert.True(zip.DirectoryExists(UPath.Root));
		Assert.Equal("b", TestPath.ReadAllText(zip, new UPath("/nested/b.txt")));
	}

	[Fact]
	public void EnumeratesEveryFileRecursively()
	{
		var path = CreateArchive(("a.txt", "a"), ("nested/b.txt", "b"), ("nested/deep/c.txt", "c"));
		using var zip = Open(path);

		Assert.Equal(3, zip.EnumerateFiles(UPath.Root, "*", SearchOption.AllDirectories).Count());
		Assert.Equal(2, zip.EnumerateFiles(new UPath("/nested"), "*", SearchOption.AllDirectories).Count());
	}

	[Fact]
	public void MissingEntryThrowsFileNotFound()
	{
		var path = CreateArchive(("a.txt", "a"));
		using var zip = Open(path);

		Assert.False(zip.FileExists(new UPath("/missing.txt")));
		Assert.ThrowsAny<IOException>(() => zip.OpenReadShared(new UPath("/missing.txt")).Dispose());
	}

	[Fact]
	public void ReadModeArchiveRejectsWrites()
	{
		var path = CreateArchive(("a.txt", "a"));
		using var zip = Open(path);

		Assert.ThrowsAny<IOException>(() => zip.OpenFile(new UPath("/b.txt"), FileMode.Create, FileAccess.Write));
	}

	[Fact]
	public void SharedReadsOpenTheSameEntryTwice()
	{
		var path = CreateArchive(("a.txt", "a"));
		using var zip = Open(path);
		var entry = new UPath("/a.txt");

		using var first = zip.OpenReadShared(entry);
		using var second = zip.OpenReadShared(entry);

		Assert.Equal("a", new StreamReader(first).ReadToEnd());
		Assert.Equal("a", new StreamReader(second).ReadToEnd());
	}

	[Fact]
	public void ExclusiveReadsConflictOnTheSameEntry()
	{
		var path = CreateArchive(("a.txt", "a"));
		using var zip = Open(path);
		var entry = new UPath("/a.txt");

		using var first = zip.OpenFile(entry, FileMode.Open, FileAccess.Read, FileShare.None);

		Assert.ThrowsAny<IOException>(() => zip.OpenFile(entry, FileMode.Open, FileAccess.Read, FileShare.None).Dispose());
	}

	[Fact]
	public void ArchivesWithoutLeadingSlashesAreUsable()
	{
		var path = CreateArchive(("plain.txt", "plain"));
		using var zip = Open(path);

		Assert.Equal("plain", TestPath.ReadAllText(zip, new UPath("/plain.txt")));
	}
}

public sealed class AssemblyResourceFileSystemTests
{
	private static Assembly TestAssembly => typeof(AssemblyResourceFileSystemTests).Assembly;

	[Fact]
	public void MapsSingleExtensionResource()
	{
		using var resourceSystem = new AssemblyResourceFileSystem(TestAssembly, "EdgeOfPlain.Tests.Assets");

		Assert.True(resourceSystem.FileExists(new UPath("/plain.txt")));
		Assert.Equal("plain", TestPath.ReadAllText(resourceSystem, new UPath("/plain.txt")).Trim());
	}

	[Fact]
	public void MapsNestedResourceWithDotsInItsName()
	{
		using var resourceSystem = new AssemblyResourceFileSystem(TestAssembly, "EdgeOfPlain.Tests.Assets");

		Assert.True(resourceSystem.FileExists(new UPath("/Contents/Multi.Part.Name.toml")));
	}

	[Fact]
	public void DefaultPrefixIsTheAssemblyAssetsFolder()
	{
		using var resourceSystem = new AssemblyResourceFileSystem(TestAssembly);

		Assert.True(resourceSystem.FileExists(new UPath("/plain.txt")));
	}

	[Fact]
	public void CoreAssemblyAssetsKeepDefaultPrefix()
	{
		using var resourceSystem = new AssemblyResourceFileSystem(typeof(Core.Launcher).Assembly);

		Assert.True(resourceSystem.FileExists(new UPath("/Contents/Tilesets/testTile.toml")));
		Assert.True(resourceSystem.FileExists(new UPath("/Textures/Tiles/testTile.png")));
	}

	[Fact]
	public void EnumeratesFoldersAndFiles()
	{
		using var resourceSystem = new AssemblyResourceFileSystem(TestAssembly);

		Assert.True(resourceSystem.DirectoryExists(new UPath("/Contents")));
		Assert.Equal(["/plain.txt"], resourceSystem
			.EnumerateFiles(UPath.Root, "*", SearchOption.TopDirectoryOnly)
			.Select(path => path.FullName));
	}

	[Fact]
	public void MissingFileThrowsFileNotFound()
	{
		using var resourceSystem = new AssemblyResourceFileSystem(TestAssembly);

		Assert.False(resourceSystem.FileExists(new UPath("/missing.txt")));
		Assert.Throws<FileNotFoundException>(() => resourceSystem.OpenReadShared(new UPath("/missing.txt")).Dispose());
	}

	[Fact]
	public void RejectsAssemblyWithoutResourcesUnderPrefix()
	{
		Assert.Throws<InvalidOperationException>(() => new AssemblyResourceFileSystem(TestAssembly, "Nothing.Here"));
	}
}