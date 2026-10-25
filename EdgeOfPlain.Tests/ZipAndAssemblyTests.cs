using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using EdgeOfPlain.Core.Framework.IO;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class ZipFileSystemTests
{
	private static ZipFileSystem Open(params (string Name, string Content)[] entries)
	{
		var buffer = new MemoryStream();
		using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
		{
			foreach (var (name, content) in entries)
			{
				var entry = archive.CreateEntry(name);
				using var writer = new StreamWriter(entry.Open());
				writer.Write(content);
			}
		}

		buffer.Position = 0;
		return new ZipFileSystem(buffer);
	}

	[Fact]
	public void ReadsEntriesAndFolders()
	{
		using var zip = Open(("a.txt", "a"), ("nested/b.txt", "b"));

		Assert.True(zip.FileExists("a.txt"));
		Assert.True(zip.FileExists("NESTED/B.TXT"));
		Assert.True(zip.DirectoryExists("nested"));
		Assert.True(zip.DirectoryExists(string.Empty));
		Assert.Equal("b", zip.ReadAllText("nested/b.txt"));
	}

	[Fact]
	public void EnumeratesChildrenOfDirectory()
	{
		using var zip = Open(("a.txt", "a"), ("nested/b.txt", "b"), ("nested/deep/c.txt", "c"));

		var root = zip.EnumerateEntries().ToArray();
		var nested = zip.EnumerateEntries("nested").ToArray();

		Assert.Equal(2, root.Length);
		Assert.Contains(root, entry => entry is { Path: "a.txt", IsDirectory: false });
		Assert.Contains(root, entry => entry is { Path: "nested", IsDirectory: true });
		Assert.Equal(2, nested.Length);
	}

	[Fact]
	public void EnumeratesAllFilesRecursively()
	{
		using var zip = Open(("a.txt", "a"), ("nested/b.txt", "b"), ("nested/deep/c.txt", "c"));

		var files = zip.EnumerateFiles(string.Empty, "*", SearchOption.AllDirectories).ToArray();

		Assert.Equal(3, files.Length);
	}

	[Fact]
	public void IgnoresEntriesThatEscapeTheRoot()
	{
		using var zip = Open(("../../evil.txt", "evil"), ("ok.txt", "ok"));

		Assert.False(zip.FileExists("evil.txt"));
		Assert.True(zip.FileExists("ok.txt"));
		Assert.Single(zip.EnumerateEntries());
	}

	[Fact]
	public void MissingEntry_ThrowsFileNotFound()
	{
		using var zip = Open(("a.txt", "a"));

		Assert.False(zip.FileExists("missing.txt"));
		Assert.Throws<FileNotFoundException>(() => zip.OpenRead("missing.txt"));
	}

	[Fact]
	public void EmptyPath_IsNotAFile()
	{
		using var zip = Open(("a.txt", "a"));

		Assert.False(zip.FileExists(string.Empty));
		Assert.True(zip.DirectoryExists(string.Empty));
	}

	[Fact]
	public void ZipIsReadOnlyAndCaseInsensitive()
	{
		using var zip = Open(("a.txt", "a"));

		Assert.True(zip.IsReadOnly);
		Assert.False(zip.IsCaseSensitive);
		Assert.Throws<NotSupportedException>(() => zip.OpenWrite("b.txt"));
	}
}

public sealed class AssemblyResourceSystemTests
{
	private static Assembly TestAssembly => typeof(AssemblyResourceSystemTests).Assembly;

	[Fact]
	public void MapsSingleExtensionResource()
	{
		using var resourceSystem = new AssemblyResourceSystem(
			TestAssembly, "EdgeOfPlain.Tests.Assets");

		Assert.True(resourceSystem.FileExists("plain.txt"));
		Assert.Equal("plain", resourceSystem.ReadAllText("plain.txt").Trim());
	}

	[Fact]
	public void MapsNestedResource()
	{
		using var resourceSystem = new AssemblyResourceSystem(
			TestAssembly, "EdgeOfPlain.Tests.Assets");

		Assert.True(resourceSystem.FileExists("Contents/Multi.Part.Name.toml"));
	}

	[Fact]
	public void CoreAssemblyAssetsKeepDefaultPrefix()
	{
		using var resourceSystem = new AssemblyResourceSystem(typeof(Core.Launcher).Assembly);

		Assert.True(resourceSystem.FileExists("Contents/Tilesets/testTile.toml"));
		Assert.True(resourceSystem.FileExists("Textures/Tiles/testTile.png"));
	}

	[Fact]
	public void RejectsAssemblyWithoutResourcesUnderPrefix()
	{
		Assert.Throws<InvalidOperationException>(() => new AssemblyResourceSystem(TestAssembly, "Nothing.Here"));
	}
}