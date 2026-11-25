using System;
using System.IO;
using System.Linq;
using EdgeOfPlain.Core.Framework.IO;
using Xunit;
using Zio;
using Zio.FileSystems;

namespace EdgeOfPlain.Tests;

public sealed class FileSystemContractTests
{
	[Fact]
	public void RelativePathsAreRejected()
	{
		var fileSystem = new MemoryFileSystem();

		Assert.ThrowsAny<ArgumentException>(() => fileSystem.FileExists(new UPath("a.txt")));
	}

	[Fact]
	public void DotSegmentsAreCollapsed()
	{
		var fileSystem = new MemoryFileSystem().AddText("/a/c.txt", "x");

		Assert.True(fileSystem.FileExists(new UPath("/a/./c.txt")));
		Assert.True(fileSystem.FileExists(new UPath("/a/b/../c.txt")));
	}

	[Fact]
	public void SubFileSystemStripsItsPrefix()
	{
		var inner = new MemoryFileSystem().AddText("/mod/assets/a.png", "a").AddText("/other/b.png", "b");
		var sub = new SubFileSystem(inner, new UPath("/mod/assets"), owned: false);

		Assert.True(sub.FileExists(new UPath("/a.png")));
		Assert.Equal("a", TestPath.ReadAllText(sub, new UPath("/a.png")));
		Assert.Equal(["/a.png"], sub.EnumerateFiles(UPath.Root, "*", SearchOption.TopDirectoryOnly).Select(path => path.FullName));
		Assert.False(sub.FileExists(new UPath("/b.png")));
	}

	[Fact]
	public void SubFileSystemForwardsWritesToTheInnerFileSystem()
	{
		using var inner = new MemoryFileSystem();
		var sub = new SubFileSystem(inner, UPath.Root, owned: false);

		sub.WriteAllText(new UPath("/a.txt"), "value");

		Assert.Equal("value", TestPath.ReadAllText(inner, new UPath("/a.txt")));
	}

	[Fact]
	public void PhysicalEnumerationOfAMissingDirectoryThrows()
	{
		using var directory = new TempDirectory();
		using var fileSystem = ZioTestExtensions.MountDirectory(directory.Path);

		Assert.ThrowsAny<IOException>(() => fileSystem.EnumerateFiles(new UPath("/missing"), "*", SearchOption.TopDirectoryOnly).ToArray());
	}

	[Fact]
	public void PhysicalMountReadsAndEnumerates()
	{
		using var directory = new TempDirectory();
		directory.Write("nested/a.txt", "hello");
		directory.Write("b.txt", "world");
		using var fileSystem = ZioTestExtensions.MountDirectory(directory.Path);

		Assert.Equal("hello", TestPath.ReadAllText(fileSystem, new UPath("/nested/a.txt")));
		Assert.Equal(2, fileSystem.EnumerateFiles(UPath.Root, "*", SearchOption.AllDirectories).Count());
	}
}

public sealed class OverlayFileSystemTests
{
	[Fact]
	public void OverrideLayerShadowsBaseFile()
	{
		var baseLayer = new MemoryFileSystem().AddText("/a.txt", "base").AddText("/b.txt", "base");
		using var overlay = new OverlayFileSystem(baseLayer);
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().AddText("/a.txt", "mod"), "mod"));

		Assert.Equal("mod", TestPath.ReadAllText(overlay, new UPath("/a.txt")));
		Assert.Equal("base", TestPath.ReadAllText(overlay, new UPath("/b.txt")));
		Assert.Single(overlay.Shadows);
	}

	[Fact]
	public void OverrideLayerCanOnlyShadowExistingFiles()
	{
		using var overlay = new OverlayFileSystem(new MemoryFileSystem().AddText("/a.txt", "base"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(
			new MemoryFileSystem().AddText("/a.txt", "mod").AddText("/new.txt", "mod"), "mod"));

		Assert.False(overlay.FileExists(new UPath("/new.txt")));
		Assert.Single(overlay.Diagnostics);
		Assert.Contains("is not found in low layers", overlay.Diagnostics[0]);
	}

	[Fact]
	public void TwoLayersShadingTheSameFileProduceConflictDiagnostic()
	{
		using var overlay = new OverlayFileSystem(new MemoryFileSystem().AddText("/a.txt", "base"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().AddText("/a.txt", "one"), "one"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().AddText("/a.txt", "two"), "two"));

		Assert.Equal("two", TestPath.ReadAllText(overlay, new UPath("/a.txt")));
		Assert.Contains(overlay.Diagnostics, message => message.Contains("simultaneously covered"));
	}

	[Fact]
	public void RemoveOverrideLayersRestoresBaseContent()
	{
		using var overlay = new OverlayFileSystem(new MemoryFileSystem().AddText("/a.txt", "base"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().AddText("/a.txt", "mod"), "mod"));
		Assert.Equal("mod", TestPath.ReadAllText(overlay, new UPath("/a.txt")));

		Assert.True(overlay.RemoveOverrideLayers("mod"));

		Assert.Equal("base", TestPath.ReadAllText(overlay, new UPath("/a.txt")));
		Assert.Empty(overlay.Shadows);
	}

	[Fact]
	public void SealedOverlayRejectsNewLayers()
	{
		using var overlay = new OverlayFileSystem(new MemoryFileSystem());
		overlay.Seal();

		Assert.Throws<InvalidOperationException>(()
			=> overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem(), "late")));
	}

	[Fact]
	public void OverlayIsReadOnly()
	{
		using var overlay = new OverlayFileSystem(new MemoryFileSystem());

		Assert.Throws<IOException>(() => overlay.OpenFile(new UPath("/a.txt"), FileMode.Create, FileAccess.Write));
	}

	[Fact]
	public void MissingFileThrowsFileNotFound()
	{
		using var overlay = new OverlayFileSystem(new MemoryFileSystem());

		Assert.Throws<FileNotFoundException>(() => overlay.OpenFile(new UPath("/missing.txt"), FileMode.Open, FileAccess.Read, FileShare.Read));
	}
}

public sealed class ModFileSystemTests
{
	[Fact]
	public void ResolveUsesNamespaceAsFirstSegment()
	{
		using var modFileSystem = new ModFileSystem();
		modFileSystem.RegisterNamespace("core", new MemoryFileSystem().AddText("/a.txt", "a"));

		Assert.True(modFileSystem.FileExists(new UPath("/core/a.txt")));
		Assert.Equal("a", TestPath.ReadAllText(modFileSystem, new UPath("/core/a.txt")));
		Assert.True(modFileSystem.DirectoryExists(new UPath("/core")));
	}

	[Fact]
	public void NamespacesAreMatchedCaseInsensitively()
	{
		using var modFileSystem = new ModFileSystem();
		modFileSystem.RegisterNamespace("core", new MemoryFileSystem().AddText("/a.txt", "a"));

		Assert.True(modFileSystem.FileExists(new UPath("/CORE/a.txt")));
		Assert.NotNull(modFileSystem.GetNamespace("CORE"));
		Assert.Equal(["core"], modFileSystem.Namespaces.Keys.ToArray());
	}

	[Fact]
	public void UnknownNamespaceIsNotVisible()
	{
		using var modFileSystem = new ModFileSystem();

		Assert.False(modFileSystem.FileExists(new UPath("/missing/a.txt")));
		Assert.False(modFileSystem.DirectoryExists(new UPath("/missing")));
		Assert.ThrowsAny<IOException>(() => modFileSystem.OpenFile(new UPath("/missing/a.txt"), FileMode.Open, FileAccess.Read, FileShare.Read).Dispose());
	}

	[Fact]
	public void NormalizeNamespaceLowercasesAndRejectsSeparators()
	{
		Assert.Equal("my-mod", ModFileSystem.NormalizeNamespace("My-Mod"));
		Assert.Throws<ArgumentException>(() => ModFileSystem.NormalizeNamespace("a b"));
		Assert.Throws<ArgumentException>(() => ModFileSystem.NormalizeNamespace("a/b"));
		Assert.Throws<ArgumentException>(() => ModFileSystem.NormalizeNamespace(" "));
	}

	[Fact]
	public void RegisterNamespaceIsCaseInsensitiveAndUnique()
	{
		using var modFileSystem = new ModFileSystem();
		modFileSystem.RegisterNamespace("Core", new MemoryFileSystem());

		Assert.Throws<InvalidOperationException>(() => modFileSystem.RegisterNamespace("core", new MemoryFileSystem()));
		Assert.NotNull(modFileSystem.GetNamespace("CORE"));
	}

	[Fact]
	public void AddOverrideLayerRequiresKnownTargetNamespace()
	{
		using var modFileSystem = new ModFileSystem();
		modFileSystem.RegisterNamespace("core", new MemoryFileSystem().AddText("/a.txt", "base"));

		Assert.True(modFileSystem.AddOverrideLayer("core", new MemoryFileSystem().AddText("/a.txt", "mod"), "mod"));
		Assert.Equal("mod", TestPath.ReadAllText(modFileSystem, new UPath("/core/a.txt")));
		Assert.False(modFileSystem.AddOverrideLayer("missing", new MemoryFileSystem(), "mod"));
	}

	[Fact]
	public void SealPreventsFurtherRegistration()
	{
		using var modFileSystem = new ModFileSystem();
		modFileSystem.Seal();

		Assert.Throws<InvalidOperationException>(() => modFileSystem.RegisterNamespace("core", new MemoryFileSystem()));
		Assert.Throws<InvalidOperationException>(() => modFileSystem.AddOverrideLayer("core", new MemoryFileSystem(), "m"));
	}

	[Fact]
	public void UnregisterNamespaceRemovesContent()
	{
		using var modFileSystem = new ModFileSystem();
		modFileSystem.RegisterNamespace("core", new MemoryFileSystem().AddText("/a.txt", "a"));

		Assert.True(modFileSystem.UnregisterNamespace("CORE"));
		Assert.False(modFileSystem.FileExists(new UPath("/core/a.txt")));
		Assert.False(modFileSystem.UnregisterNamespace("core"));
	}

	[Fact]
	public void RemoveOverrideLayersAppliesToAllNamespaces()
	{
		using var modFileSystem = new ModFileSystem();
		modFileSystem.RegisterNamespace("core", new MemoryFileSystem().AddText("/a.txt", "base"));
		modFileSystem.AddOverrideLayer("core", new MemoryFileSystem().AddText("/a.txt", "mod"), "mod");

		Assert.Equal(1, modFileSystem.RemoveOverrideLayers("MOD"));
		Assert.Equal("base", TestPath.ReadAllText(modFileSystem, new UPath("/core/a.txt")));
	}
}