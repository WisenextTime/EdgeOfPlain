using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdgeOfPlain.Core.Framework.IO;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class OverlayLayerTests
{
	[Fact]
	public void Layers_StartWithNamedBaseLayer()
	{
		var overlay = new OverlayFileSystem(new MemoryFileSystem(), "content");

		Assert.Same(overlay.Layers[0], overlay.BaseLayer);
		Assert.Equal("content", overlay.BaseLayer.Name);
		Assert.Single(overlay.Layers);
		Assert.True(overlay.IsReadOnly);
		Assert.Empty(overlay.Diagnostics);
		Assert.Empty(overlay.Shadows);
	}

	[Fact]
	public void AddOverrideLayer_RejectsNull()
	{
		var overlay = new OverlayFileSystem(new MemoryFileSystem());

		Assert.Throws<ArgumentNullException>(() => overlay.AddOverrideLayer(null!));
	}

	[Fact]
	public void CaseSensitivityAndReadOnlyFlagFollowBaseLayer()
	{
		var sensitive = new OverlayFileSystem(new MemoryFileSystem().Add("A.txt", "a"));
		var insensitive = new OverlayFileSystem(new MemoryFileSystem(false).Add("A.txt", "a"));

		Assert.True(sensitive.IsCaseSensitive);
		Assert.False(insensitive.IsCaseSensitive);
		Assert.Equal("a", insensitive.ReadAllText("A.TXT"));
		Assert.False(sensitive.FileExists("a.txt"));
	}

	[Fact]
	public void RemoveOverrideLayers_KeepsNewerLayersUntilTheyGo()
	{
		var overlay = new OverlayFileSystem(new MemoryFileSystem().Add("a.txt", "base"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().Add("a.txt", "one"), "one"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().Add("a.txt", "two"), "two"));

		Assert.Equal(2, overlay.Shadows["a.txt"]);
		Assert.True(overlay.RemoveOverrideLayers("one"));
		Assert.Equal("two", overlay.ReadAllText("a.txt"));
		Assert.Equal(1, overlay.Shadows["a.txt"]);

		Assert.True(overlay.RemoveOverrideLayers("TWO"));
		Assert.Equal("base", overlay.ReadAllText("a.txt"));
		Assert.False(overlay.RemoveOverrideLayers("two"));
		Assert.Empty(overlay.Shadows);
	}

	[Fact]
	public void RemovingLayer_DiscardsItsDiagnostics()
	{
		var overlay = new OverlayFileSystem(new MemoryFileSystem().Add("a.txt", "base"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().Add("new.txt", "x"), "extra"));
		Assert.Single(overlay.Diagnostics);
		Assert.False(overlay.FileExists("new.txt"));

		overlay.RemoveOverrideLayers("extra");

		Assert.Empty(overlay.Diagnostics);
	}

	[Fact]
	public void SealedOverlay_ServesContentButRejectsLayerChanges()
	{
		var overlay = new OverlayFileSystem(new MemoryFileSystem().Add("a.txt", "base"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().Add("a.txt", "mod"), "mod"));

		overlay.Seal();
		overlay.Seal();

		Assert.True(overlay.IsSealed);
		Assert.Equal("mod", overlay.ReadAllText("a.txt"));
		Assert.Throws<InvalidOperationException>(() => overlay.RemoveOverrideLayers("mod"));
		Assert.Throws<InvalidOperationException>(()
			=> overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem(), "late")));
	}

	[Fact]
	public void DisposedOverlay_RejectsLayerChanges()
	{
		var overlay = new OverlayFileSystem(new MemoryFileSystem());
		overlay.Dispose();

		Assert.Throws<ObjectDisposedException>(()
			=> overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem(), "late")));
		Assert.Throws<ObjectDisposedException>(() => overlay.RemoveOverrideLayers("late"));
	}

	[Fact]
	public void DirectoryExists_IgnoresOverrideLayers()
	{
		var overlay = new OverlayFileSystem(new MemoryFileSystem().Add("a.txt", "a"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().Add("dir/x.txt", "x"), "mod"));

		Assert.True(overlay.DirectoryExists(string.Empty));
		Assert.False(overlay.DirectoryExists("dir"));
		Assert.False(overlay.FileExists("dir/x.txt"));
	}
}

public sealed class SubFileSystemPathTests
{
	[Fact]
	public void Prefix_IsNormalized()
	{
		var inner = new MemoryFileSystem();
		var sub = new SubFileSystem(inner, "/mod//assets/");

		Assert.Equal("mod/assets", sub.Prefix);
		Assert.Same(inner, sub.Inner);
		Assert.Equal(string.Empty, new SubFileSystem(inner).Prefix);
	}

	[Fact]
	public void EmptyPrefix_PassesPathsThrough()
	{
		var inner = new MemoryFileSystem().Add("a.txt", "a");
		var sub = new SubFileSystem(inner);

		Assert.True(sub.FileExists("a.txt"));
		Assert.Equal("a", sub.ReadAllText("a.txt"));
		Assert.Equal(new[] { "a.txt" }, sub.EnumerateFiles(string.Empty).ToArray());
	}

	[Fact]
	public void DirectoryMapping_UsesPrefix()
	{
		var inner = new MemoryFileSystem().Add("mod/assets/a.png", "a");
		var sub = new SubFileSystem(inner, "mod/assets");

		Assert.True(sub.DirectoryExists(string.Empty));
		Assert.True(sub.DirectoryExists("."));
		Assert.False(sub.DirectoryExists("missing"));
		Assert.False(sub.FileExists("missing.png"));
		Assert.Throws<FileNotFoundException>(() => sub.OpenRead("missing.png"));
	}

	[Fact]
	public void EnumerateEntries_IsRelativeToPrefix()
	{
		var inner = new MemoryFileSystem()
			.Add("mod/assets/a.png", "a")
			.Add("mod/assets/sub/b.png", "b")
			.Add("other/c.png", "c");
		var sub = new SubFileSystem(inner, "mod/assets");

		var root = sub.EnumerateEntries().ToArray();
		var nested = sub.EnumerateEntries("sub").ToArray();

		Assert.Contains(root, entry => entry is { Path: "a.png", IsDirectory: false });
		Assert.Contains(root, entry => entry is { Path: "sub", IsDirectory: true });
		Assert.Equal(new[] { "sub/b.png" }, nested.Select(entry => entry.Path).ToArray());
		Assert.Equal(
			new[] { "a.png", "sub/b.png" },
			sub.EnumerateFiles(string.Empty, "*", SearchOption.AllDirectories).OrderBy(file => file).ToArray());
		Assert.DoesNotContain(sub.EnumerateFiles(string.Empty, "*", SearchOption.AllDirectories), file => file.StartsWith("other"));
	}

	[Fact]
	public void TryGetPhysicalPath_DelegatesToInner()
	{
		using var directory = new TempDirectory();
		directory.Write("mod/assets/a.txt", "a");
		using var physical = new PhysicalFileSystem(directory.Path);
		var sub = new SubFileSystem(physical, "mod/assets");

		Assert.True(sub.TryGetPhysicalPath("a.txt", out var path));
		Assert.Equal(Path.Combine(directory.Path, "mod", "assets", "a.txt"), path);
		Assert.False(new SubFileSystem(new MemoryFileSystem(), "mod").TryGetPhysicalPath("a.txt", out _));
	}

	[Fact]
	public void FlagsDelegateToInner()
	{
		var inner = new MemoryFileSystem(false);
		var sub = new SubFileSystem(inner, "mod");

		Assert.Equal(inner.IsReadOnly, sub.IsReadOnly);
		Assert.Equal(inner.IsCaseSensitive, sub.IsCaseSensitive);
		Assert.False(sub.IsCaseSensitive);
	}
}

public sealed class ModFileSystemNamespaceTests
{
	[Fact]
	public void ReservedNamespaces_ContainBaseCaseInsensitively()
	{
		Assert.Contains("BASE", ModFileSystem.ReservedNamespaces);
		Assert.Contains("base", ModFileSystem.ReservedNamespaces);
		Assert.Single(ModFileSystem.ReservedNamespaces);
	}

	[Fact]
	public void RegisterNamespace_RejectsInvalidNamesAndNullContent()
	{
		var fileSystem = new ModFileSystem();

		Assert.Throws<ArgumentNullException>(() => fileSystem.RegisterNamespace("core", null!));
		Assert.Throws<ArgumentException>(() => fileSystem.RegisterNamespace("bad name", new MemoryFileSystem()));
		Assert.Throws<ArgumentException>(() => fileSystem.RegisterNamespace("bad:name", new MemoryFileSystem()));
		Assert.Throws<ArgumentException>(() => fileSystem.RegisterNamespace(" ", new MemoryFileSystem()));
	}

	[Fact]
	public void RegisterNamespace_StoresNormalizedName()
	{
		var fileSystem = new ModFileSystem();

		fileSystem.RegisterNamespace("My-Mod", new MemoryFileSystem());

		Assert.Equal(new[] { "my-mod" }, fileSystem.Namespaces.Keys.ToArray());
		Assert.NotNull(fileSystem.GetNamespace("my-mod"));
	}

	[Fact]
	public void GetNamespace_IsCaseInsensitiveAndNullWhenUnknown()
	{
		var fileSystem = new ModFileSystem();

		Assert.Null(fileSystem.GetNamespace("missing"));

		var registered = fileSystem.RegisterNamespace("core", new MemoryFileSystem());
		Assert.Same(registered, fileSystem.GetNamespace("CORE"));
	}

	[Fact]
	public void AddOverrideLayer_ValidatesLayerNameAndTarget()
	{
		var fileSystem = new ModFileSystem();
		fileSystem.RegisterNamespace("core", new MemoryFileSystem().Add("a.txt", "base"));

		Assert.Throws<ArgumentNullException>(() => fileSystem.AddOverrideLayer("core", null!, "mod"));
		Assert.Throws<ArgumentException>(() => fileSystem.AddOverrideLayer("core", new MemoryFileSystem(), "bad name"));
		Assert.False(fileSystem.AddOverrideLayer("other", new MemoryFileSystem(), "mod"));
	}

	[Fact]
	public void AddOverrideLayer_TargetNamespaceIsCaseInsensitive()
	{
		var fileSystem = new ModFileSystem();
		fileSystem.RegisterNamespace("core", new MemoryFileSystem().Add("a.txt", "base"));

		Assert.True(fileSystem.AddOverrideLayer("CORE", new MemoryFileSystem().Add("a.txt", "mod"), "MOD"));

		Assert.Equal("mod", fileSystem.ReadAllText("core/a.txt"));
		Assert.False(fileSystem.GetNamespace("core")!.IsSealed);
	}

	[Fact]
	public void Seal_IsIdempotentAndFreezesEveryOverlay()
	{
		var fileSystem = new ModFileSystem();
		var overlay = fileSystem.RegisterNamespace("core", new MemoryFileSystem().Add("a.txt", "base"));

		fileSystem.Seal();
		fileSystem.Seal();

		Assert.True(fileSystem.IsSealed);
		Assert.True(overlay.IsSealed);
		Assert.Equal("base", fileSystem.ReadAllText("core/a.txt"));
		Assert.Throws<InvalidOperationException>(() => fileSystem.UnregisterNamespace("core"));
		Assert.Throws<InvalidOperationException>(() => fileSystem.RemoveOverrideLayers("mod"));
		Assert.Throws<InvalidOperationException>(() => fileSystem.RegisterNamespace("late", new MemoryFileSystem()));
	}

	[Fact]
	public void EnumerateEntries_PrefixesNamespaceToPaths()
	{
		var fileSystem = new ModFileSystem();
		fileSystem.RegisterNamespace("core", new MemoryFileSystem()
			.Add("a.txt", "a")
			.Add("sub/b.txt", "b"));

		Assert.Equal(
			new[] { "core/a.txt", "core/sub" },
			fileSystem.EnumerateEntries("core").Select(entry => entry.Path).ToArray());
		Assert.Equal(
			new[] { "core/sub/b.txt" },
			fileSystem.EnumerateEntries("core/sub").Select(entry => entry.Path).ToArray());
		Assert.Equal(
			new[] { "core/a.txt", "core/sub/b.txt" },
			fileSystem.EnumerateFiles("core", "*", SearchOption.AllDirectories).OrderBy(path => path).ToArray());
	}

	[Fact]
	public void OpenRead_MissingFileInNamespace_ThrowsFileNotFound()
	{
		var fileSystem = new ModFileSystem();
		fileSystem.RegisterNamespace("core", new MemoryFileSystem().Add("a.txt", "a"));

		Assert.Throws<FileNotFoundException>(() => fileSystem.OpenRead("core/missing.txt"));
		Assert.False(fileSystem.FileExists("core/missing.txt"));
		Assert.False(fileSystem.DirectoryExists("missing"));
	}

	[Fact]
	public void FileSystem_IsReadOnlyAndCaseInsensitive()
	{
		var fileSystem = new ModFileSystem();

		Assert.True(fileSystem.IsReadOnly);
		Assert.False(fileSystem.IsCaseSensitive);
		Assert.Throws<NotSupportedException>(() => fileSystem.OpenWrite("core/a.txt"));
	}
}
