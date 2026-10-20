using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EdgeOfPlain.Core.Framework.IO;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class FileSystemTests
{
	[Fact]
	public void Normalize_CollapsesDotSegments()
	{
		var fileSystem = new MemoryFileSystem().Add("a/./b/../c.txt", "x");

		Assert.True(fileSystem.FileExists("a/c.txt"));
		Assert.True(fileSystem.FileExists("/a//c.txt"));
	}

	[Fact]
	public void Normalize_RejectsEscapingRoot()
	{
		var fileSystem = new MemoryFileSystem();

		Assert.Throws<ArgumentException>(() => fileSystem.FileExists("../secret.txt"));
		Assert.Throws<ArgumentException>(() => fileSystem.FileExists("a/../../secret.txt"));
	}

	[Fact]
	public void Normalize_RejectsDriveLetters()
	{
		var fileSystem = new MemoryFileSystem();

		Assert.Throws<ArgumentException>(() => fileSystem.FileExists("C:/windows/system32"));
	}

	[Fact]
	public void EnumerateFiles_ExtensionPatternNeedsWildcard()
	{
		var fileSystem = new MemoryFileSystem().Add("x.toml", "a").Add("y.txt", "b");

		Assert.Equal(new[] { "x.toml" }, fileSystem.EnumerateFiles(string.Empty, "*.toml").ToArray());
		Assert.Empty(fileSystem.EnumerateFiles(string.Empty, "toml"));
	}

	[Fact]
	public void EnumerateFiles_AllDirectories_WalksNestedFolders()
	{
		var fileSystem = new MemoryFileSystem()
			.Add("a.toml", "1")
			.Add("nested/b.toml", "2")
			.Add("nested/deeper/c.toml", "3");

		var top = fileSystem.EnumerateFiles(string.Empty, "*.toml", SearchOption.TopDirectoryOnly).ToArray();
		var all = fileSystem.EnumerateFiles(string.Empty, "*.toml", SearchOption.AllDirectories).ToArray();

		Assert.Equal(new[] { "a.toml" }, top);
		Assert.Equal(3, all.Length);
	}

	[Fact]
	public void EnumerateDirectories_ReportsOnlyDirectories()
	{
		var fileSystem = new MemoryFileSystem().Add("nested/b.toml", "2").Add("root.txt", "1");

		Assert.Equal(new[] { "nested" }, fileSystem.EnumerateDirectories(string.Empty).ToArray());
		Assert.Empty(fileSystem.EnumerateDirectories(string.Empty, "root*"));
	}

	[Fact]
	public void ReadAllText_HandlesUtf8Bom()
	{
		var fileSystem = new MemoryFileSystem();
		var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("héllo")).ToArray();
		fileSystem.Add("bom.txt", bytes);

		Assert.Equal("héllo", fileSystem.ReadAllText("bom.txt"));
	}

	[Fact]
	public void WriteAllText_DoesNotEmitBom()
	{
		var fileSystem = new MemoryFileSystem();
		fileSystem.WriteAllText("a.txt", "abc");

		Assert.Equal("abc"u8.ToArray(), fileSystem.Files["a.txt"]);
	}

	[Fact]
	public async Task WriteAllTextAsync_PersistsContent()
	{
		var fileSystem = new MemoryFileSystem();

		await fileSystem.WriteAllTextAsync("a.txt", "hello", TestContext.Current.CancellationToken);
		var text = await fileSystem.ReadAllTextAsync("a.txt", TestContext.Current.CancellationToken);

		Assert.Equal("hello", text);
		Assert.Equal("hello", fileSystem.ReadAllText("a.txt"));
	}

	[Fact]
	public void CopyFile_RefusesExistingTargetWithoutOverwrite()
	{
		var fileSystem = new MemoryFileSystem().Add("a.txt", "a").Add("b.txt", "b");

		Assert.Throws<IOException>(() => fileSystem.CopyFile("a.txt", "b.txt"));
		fileSystem.CopyFile("a.txt", "b.txt", true);

		Assert.Equal("a", fileSystem.ReadAllText("b.txt"));
	}

	[Fact]
	public void MoveFile_DeletesSource()
	{
		var fileSystem = new MemoryFileSystem().Add("a.txt", "a");

		fileSystem.MoveFile("a.txt", "b.txt");

		Assert.False(fileSystem.FileExists("a.txt"));
		Assert.Equal("a", fileSystem.ReadAllText("b.txt"));
	}

	[Fact]
	public void GetFileLength_ReturnsByteCount()
	{
		var fileSystem = new MemoryFileSystem().Add("a.txt", "hello");

		Assert.Equal(5L, fileSystem.GetFileLength("a.txt"));
	}

	[Fact]
	public void DisposedFileSystem_ThrowsOnUse()
	{
		var fileSystem = new MemoryFileSystem();
		fileSystem.Dispose();

		Assert.True(fileSystem.IsDisposed);
		Assert.Throws<ObjectDisposedException>(() => fileSystem.FileExists("a.txt"));
	}

	[Fact]
	public void SubFileSystem_MapsPrefixAndStripsIt()
	{
		var inner = new MemoryFileSystem()
			.Add("mod/assets/a.png", "a")
			.Add("other/b.png", "b");
		var sub = new SubFileSystem(inner, "mod/assets");

		Assert.True(sub.FileExists("a.png"));
		Assert.Equal("a", sub.ReadAllText("a.png"));
		Assert.Equal(new[] { "a.png" }, sub.EnumerateFiles(string.Empty).ToArray());
		Assert.False(sub.FileExists("b.png"));
	}

	[Fact]
	public void SubFileSystem_IsNotWritableThroughTheWrapper()
	{
		var inner = new MemoryFileSystem();
		var sub = new SubFileSystem(inner);

		Assert.False(sub.IsReadOnly);
		Assert.Throws<NotSupportedException>(() => sub.OpenWrite("a.txt"));
	}

	[Fact]
	public void PhysicalFileSystem_ReadsAndEnumerates()
	{
		using var directory = new TempDirectory();
		directory.Write("nested/a.txt", "hello");
		directory.Write("b.txt", "world");

		using var fileSystem = new PhysicalFileSystem(directory.Path);

		Assert.True(fileSystem.FileExists("nested/a.txt"));
		Assert.Equal("hello", fileSystem.ReadAllText("nested/a.txt"));
		Assert.Equal(2, fileSystem.EnumerateFiles(string.Empty, "*", SearchOption.AllDirectories).Count());
	}

	[Fact]
	public void PhysicalFileSystem_EnumerateMissingDirectory_ReturnsEmpty()
	{
		using var directory = new TempDirectory();
		using var fileSystem = new PhysicalFileSystem(directory.Path);

		Assert.Empty(fileSystem.EnumerateEntries("missing"));
	}

	[Fact]
	public void TryGetPhysicalPath_ReportsAbsoluteLocation()
	{
		using var directory = new TempDirectory();
		using var fileSystem = new PhysicalFileSystem(directory.Path);

		Assert.True(fileSystem.TryGetPhysicalPath("a/b.txt", out var path));
		Assert.Equal(Path.Combine(directory.Path, "a", "b.txt"), path);
	}
}

public sealed class OverlayFileSystemTests
{
	[Fact]
	public void OverrideLayer_ShadowsBaseFile()
	{
		var baseLayer = new MemoryFileSystem().Add("a.txt", "base").Add("b.txt", "base");
		var overlay = new OverlayFileSystem(baseLayer);
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().Add("a.txt", "mod"), "mod"));

		Assert.Equal("mod", overlay.ReadAllText("a.txt"));
		Assert.Equal("base", overlay.ReadAllText("b.txt"));
		Assert.Single(overlay.Shadows);
	}

	[Fact]
	public void OverrideLayer_CanOnlyShadowExistingFiles()
	{
		var overlay = new OverlayFileSystem(new MemoryFileSystem().Add("a.txt", "base"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(
			new MemoryFileSystem().Add("a.txt", "mod").Add("new.txt", "mod"), "mod"));

		Assert.False(overlay.FileExists("new.txt"));
		Assert.Single(overlay.Diagnostics);
		Assert.Contains("is not found in low layers", overlay.Diagnostics[0]);
	}

	[Fact]
	public void TwoLayersShadingTheSameFile_ProduceConflictDiagnostic()
	{
		var overlay = new OverlayFileSystem(new MemoryFileSystem().Add("a.txt", "base"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().Add("a.txt", "one"), "one"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().Add("a.txt", "two"), "two"));

		Assert.Equal("two", overlay.ReadAllText("a.txt"));
		Assert.Contains(overlay.Diagnostics, message => message.Contains("simultaneously covered"));
	}

	[Fact]
	public void RemoveOverrideLayers_RestoresBaseContent()
	{
		var overlay = new OverlayFileSystem(new MemoryFileSystem().Add("a.txt", "base"));
		overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem().Add("a.txt", "mod"), "mod"));
		Assert.Equal("mod", overlay.ReadAllText("a.txt"));

		Assert.True(overlay.RemoveOverrideLayers("mod"));

		Assert.Equal("base", overlay.ReadAllText("a.txt"));
		Assert.Empty(overlay.Shadows);
	}

	[Fact]
	public void SealedOverlay_RejectsNewLayers()
	{
		var overlay = new OverlayFileSystem(new MemoryFileSystem());
		overlay.Seal();

		Assert.Throws<InvalidOperationException>(()
			=> overlay.AddOverrideLayer(new OverlayFileSystem.Layer(new MemoryFileSystem(), "late")));
	}

	[Fact]
	public void MissingFile_ThrowsFileNotFound()
	{
		var overlay = new OverlayFileSystem(new MemoryFileSystem());

		Assert.Throws<FileNotFoundException>(() => overlay.OpenRead("missing.txt"));
	}
}

public sealed class ModFileSystemTests
{
	[Fact]
	public void Resolve_UsesNamespaceAsFirstSegment()
	{
		var modFileSystem = new ModFileSystem();
		modFileSystem.RegisterNamespace("core", new MemoryFileSystem().Add("a.txt", "a"));

		Assert.True(modFileSystem.FileExists("core/a.txt"));
		Assert.Equal("a", modFileSystem.ReadAllText("core/a.txt"));
		Assert.True(modFileSystem.DirectoryExists("core"));
	}

	[Fact]
	public void Resolve_UnknownNamespace_Throws()
	{
		var modFileSystem = new ModFileSystem();

		Assert.False(modFileSystem.FileExists("missing/a.txt"));
		Assert.Throws<FileNotFoundException>(() => modFileSystem.OpenRead("missing/a.txt"));
		Assert.Throws<FileNotFoundException>(() => modFileSystem.OpenRead("no-namespace"));
		Assert.Throws<ArgumentException>(() => modFileSystem.OpenRead(string.Empty));
	}

	[Fact]
	public void NormalizeNamespace_LowercasesAndRejectsSeparators()
	{
		Assert.Equal("my-mod", ModFileSystem.NormalizeNamespace("My-Mod"));
		Assert.Throws<ArgumentException>(() => ModFileSystem.NormalizeNamespace("a b"));
		Assert.Throws<ArgumentException>(() => ModFileSystem.NormalizeNamespace("a/b"));
		Assert.Throws<ArgumentException>(() => ModFileSystem.NormalizeNamespace(" "));
	}

	[Fact]
	public void RegisterNamespace_IsCaseInsensitiveAndUnique()
	{
		var modFileSystem = new ModFileSystem();
		modFileSystem.RegisterNamespace("Core", new MemoryFileSystem());

		Assert.Throws<InvalidOperationException>(() => modFileSystem.RegisterNamespace("core", new MemoryFileSystem()));
		Assert.NotNull(modFileSystem.GetNamespace("CORE"));
	}

	[Fact]
	public void AddOverrideLayer_RequiresKnownTargetNamespace()
	{
		var modFileSystem = new ModFileSystem();
		modFileSystem.RegisterNamespace("core", new MemoryFileSystem().Add("a.txt", "base"));

		Assert.True(modFileSystem.AddOverrideLayer("core", new MemoryFileSystem().Add("a.txt", "mod"), "mod"));
		Assert.Equal("mod", modFileSystem.ReadAllText("core/a.txt"));
		Assert.False(modFileSystem.AddOverrideLayer("missing", new MemoryFileSystem(), "mod"));
	}

	[Fact]
	public void Seal_PreventsFurtherRegistration()
	{
		var modFileSystem = new ModFileSystem();
		modFileSystem.Seal();

		Assert.Throws<InvalidOperationException>(() => modFileSystem.RegisterNamespace("core", new MemoryFileSystem()));
		Assert.Throws<InvalidOperationException>(() => modFileSystem.AddOverrideLayer("core", new MemoryFileSystem(), "m"));
	}

	[Fact]
	public void UnregisterNamespace_RemovesContent()
	{
		var modFileSystem = new ModFileSystem();
		modFileSystem.RegisterNamespace("core", new MemoryFileSystem().Add("a.txt", "a"));

		Assert.True(modFileSystem.UnregisterNamespace("CORE"));
		Assert.False(modFileSystem.FileExists("core/a.txt"));
		Assert.False(modFileSystem.UnregisterNamespace("core"));
	}

	[Fact]
	public void RemoveOverrideLayers_AppliesToAllNamespaces()
	{
		var modFileSystem = new ModFileSystem();
		modFileSystem.RegisterNamespace("core", new MemoryFileSystem().Add("a.txt", "base"));
		modFileSystem.AddOverrideLayer("core", new MemoryFileSystem().Add("a.txt", "mod"), "mod");

		Assert.Equal(1, modFileSystem.RemoveOverrideLayers("MOD"));
		Assert.Equal("base", modFileSystem.ReadAllText("core/a.txt"));
	}
}