using System;
using System.IO;
using System.Text;
using EdgeOfPlain.Core.Framework.IO;
using Xunit;
using Zio;
using Zio.FileSystems;

[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace EdgeOfPlain.Tests;

internal sealed class TempDirectory : IDisposable
{
	public string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "test-temp", Guid.NewGuid().ToString("N"));

	public TempDirectory() => Directory.CreateDirectory(Path);

	public string Write(string relative, string content)
	{
		var full = System.IO.Path.Combine(Path, relative);
		Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
		File.WriteAllText(full, content);
		return full;
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(Path, true);
		}
		catch(IOException)
		{
		}
		catch(UnauthorizedAccessException)
		{
		}
	}
}

/// <summary>
/// Boots the global <see cref="FileIO"/> with throw-away directories and a memory backed
/// <c>/assets</c> mount. Tests are serialized, so the global mount table can be reset freely.
/// </summary>
internal sealed class GameIoScope : IDisposable
{
	public GameIoScope(bool mountAssets = true)
	{
		FileIO.Reset();
		FileIO.Initialize(Data.Path, Mods.Path);
		if (mountAssets) FileIO.Mount(FileIO.Assets, Assets);
	}

	public TempDirectory Data { get; } = new();

	public TempDirectory Mods { get; } = new();

	public MemoryFileSystem Assets { get; } = new();

	public IFileSystem Root => FileIO.Root;

	public UPath this[string assetPath] => FileIO.Assets / assetPath.TrimStart('/');

	public void Dispose()
	{
		FileIO.Reset();
		Assets.Dispose();
		Data.Dispose();
		Mods.Dispose();
	}
}

internal static class ZioTestExtensions
{
	/// <summary>Writes <paramref name="text"/> and creates the parent directory when it is missing.</summary>
	public static IFileSystem AddText(this IFileSystem fileSystem, UPath path, string text)
	{
		fileSystem.CreateParentDirectory(path);
		fileSystem.WriteAllText(path, text);
		return fileSystem;
	}

	public static IFileSystem AddText(this IFileSystem fileSystem, string relativePath, string text)
		=> fileSystem.AddText(TestPath.Of(relativePath), text);

	public static IFileSystem AddBytes(this IFileSystem fileSystem, string relativePath, byte[] content)
	{
		var path = TestPath.Of(relativePath);
		fileSystem.CreateParentDirectory(path);
		fileSystem.WriteAllBytes(path, content);
		return fileSystem;
	}

	/// <summary>Mounts a host directory as a standalone file system rooted at that directory.</summary>
	public static IFileSystem MountDirectory(string directory)
	{
		var physical = new PhysicalFileSystem();
		return physical.GetOrCreateSubFileSystem(physical.ConvertPathFromInternal(Path.GetFullPath(directory)));
	}
}

internal static class TestPath
{
	public static UPath Of(string relativePath) => new("/" + relativePath.Replace('\\', '/').TrimStart('/'));

	public static string ReadAllText(IFileSystem fileSystem, UPath path)
	{
		using var stream = fileSystem.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		using var reader = new StreamReader(stream, Encoding.UTF8, true);
		return reader.ReadToEnd();
	}
}