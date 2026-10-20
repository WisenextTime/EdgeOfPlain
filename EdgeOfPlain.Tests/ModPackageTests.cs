using System;
using System.IO;
using System.IO.Compression;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Mods.Loading;
using Xunit;

namespace EdgeOfPlain.Tests;

internal static class ZipFactory
{
	public static string Create(TempDirectory directory, string fileName, params (string Name, string Content)[] entries)
	{
		var path = Path.Combine(directory.Path, fileName);
		using var stream = File.Create(path);
		using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

		foreach (var (name, content) in entries)
		{
			var entry = archive.CreateEntry(name);
			using var writer = new StreamWriter(entry.Open());
			writer.Write(content);
		}

		return path;
	}

	public const string Manifest = """
	                               id = "CoolMod"
	                               version = "1.2.3"
	                               author = "tester"
	                               description = "test mod"
	                               """;
}

public sealed class ModPackageTests
{
	[Fact]
	public void Open_ReadsManifestAndAssets()
	{
		using var directory = new TempDirectory();
		var zip = ZipFactory.Create(directory, "a.zip",
			("mod.toml", ZipFactory.Manifest),
			("Asset/a.txt", "asset"));

		using var package = ModPackage.Open(zip);

		Assert.Equal("coolmod", package.ModId);
		Assert.True(package.HasAssets);
		Assert.Equal("asset", package.Assets.ReadAllText("a.txt"));
		Assert.NotNull(package.Info);
		Assert.Equal("1.2.3", package.Info!.Value.Version.ToString());
	}

	[Fact]
	public void Open_MissingManifest_Throws()
	{
		using var directory = new TempDirectory();
		var zip = ZipFactory.Create(directory, "a.zip", ("Asset/a.txt", "asset"));

		Assert.Throws<InvalidDataException>(() => ModPackage.Open(zip));
	}

	[Fact]
	public void Open_ReservedId_Throws()
	{
		using var directory = new TempDirectory();
		var zip = ZipFactory.Create(directory, "a.zip", ("mod.toml", "id = \"base\"\nversion = \"1.0.0\""));

		Assert.Throws<InvalidDataException>(() => ModPackage.Open(zip));
	}

	[Fact]
	public void Open_ExplicitIdOverridesManifest()
	{
		using var directory = new TempDirectory();
		var zip = ZipFactory.Create(directory, "a.zip", ("mod.toml", ZipFactory.Manifest));

		using var package = ModPackage.Open(zip, "Chosen");

		Assert.Equal("chosen", package.ModId);
	}

	[Fact]
	public void Open_WithoutAssetDirectory_HasNoAssets()
	{
		using var directory = new TempDirectory();
		var zip = ZipFactory.Create(directory, "a.zip", ("mod.toml", ZipFactory.Manifest), ("other/a.txt", "x"));

		using var package = ModPackage.Open(zip);

		Assert.False(package.HasAssets);
		Assert.Empty(package.Assets.EnumerateFiles(string.Empty));
	}

	[Fact]
	public void Open_MapsOverriderDirectories()
	{
		using var directory = new TempDirectory();
		var zip = ZipFactory.Create(directory, "a.zip",
			("mod.toml", ZipFactory.Manifest),
			("Overrider/base/a.txt", "override"));

		using var package = ModPackage.Open(zip);

		Assert.True(package.Overriders.ContainsKey("base"));
		Assert.Equal("override", package.Overriders["base"].ReadAllText("a.txt"));
	}

	[Fact]
	public void Open_UnknownModIdMustComeFromManifest()
	{
		using var directory = new TempDirectory();
		var zip = ZipFactory.Create(directory, "a.zip", ("mod.toml", ZipFactory.Manifest));

		using var package = ModPackage.Open(zip);

		Assert.Equal("coolmod", package.ModId);
	}
}

public sealed class ModExtractorTests
{
	private const string Head = "eop-extract";

	[Fact]
	public void Extract_CopiesManagedFilesAndWritesMarker()
	{
		using var directory = new TempDirectory();
		var zip = ZipFactory.Create(directory, "a.zip",
			("mod.toml", ZipFactory.Manifest),
			("lib/a.dll", "fake"),
			("Asset/a.txt", "asset"));
		var path = directory.Path;
		FileIO.RegisterFileSystem(Head, () => new PhysicalFileSystem(Path.Combine(path, "cache")));
		try
		{
			using var package = ModPackage.Open(zip);

			var extracted = ModExtractor.Extract(package, $"{Head}://cache");

			Assert.NotNull(extracted);
			Assert.True(File.Exists(Path.Combine(extracted, "lib", "a.dll")));
			Assert.False(File.Exists(Path.Combine(extracted, "Asset", "a.txt")));
			Assert.Equal(extracted, ModExtractor.Extract(package, $"{Head}://cache"));
		}
		finally
		{
			FileIO.UnregisterFileSystem(Head);
		}
	}

	[Fact]
	public void Extract_WithoutManagedFiles_ReturnsNull()
	{
		using var directory = new TempDirectory();
		var zip = ZipFactory.Create(directory, "a.zip",
			("mod.toml", ZipFactory.Manifest),
			("Asset/a.txt", "asset"));
		var path = directory.Path;
		FileIO.RegisterFileSystem(Head + "2", () => new PhysicalFileSystem(Path.Combine(path, "cache")));
		try
		{
			using var package = ModPackage.Open(zip);

			Assert.Null(ModExtractor.Extract(package, $"{Head}2://cache"));
		}
		finally
		{
			FileIO.UnregisterFileSystem(Head + "2");
		}
	}

	[Fact]
	public void Extract_RequiresPhysicalCacheRoot()
	{
		using var directory = new TempDirectory();
		var zip = ZipFactory.Create(directory, "a.zip",
			("mod.toml", ZipFactory.Manifest),
			("a.dll", "fake"));

		FileIO.RegisterFileSystem(Head + "3", () => new MemoryFileSystem());
		try
		{
			using var package = ModPackage.Open(zip);

			Assert.Throws<InvalidOperationException>(() => ModExtractor.Extract(package, $"{Head}3://cache"));
		}
		finally
		{
			FileIO.UnregisterFileSystem(Head + "3");
		}
	}
}