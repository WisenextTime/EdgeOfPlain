using System;
using System.IO;

namespace EdgeOfPlain.Client.Tests;

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

	public string Write(string relative, byte[] content)
	{
		var full = System.IO.Path.Combine(Path, relative);
		Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
		File.WriteAllBytes(full, content);
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

internal static class TestAssets
{
	public const string OnePixelPngBase64 =
		"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

	public static byte[] OnePixelPng() => Convert.FromBase64String(OnePixelPngBase64);

	public const string OnePixelAtlas = """
	                                   [[Sprite]]
	                                   id = "main"
	                                   x = 0
	                                   y = 0
	                                   width = 1
	                                   height = 1
	                                   """;
}
