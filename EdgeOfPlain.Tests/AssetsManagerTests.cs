using System;
using System.IO;
using System.Threading.Tasks;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Resource;
using Xunit;

namespace EdgeOfPlain.Tests;

internal sealed record TextProbe(string Text);

internal sealed record OtherTextProbe(string Text);

public sealed class AssetsManagerTests
{
	[Fact]
	public void RegisterResourceOperator_ReportsRegistrationAndRejectsDuplicates()
	{
		AssetsManager.Clear();
		try
		{
			var resourceOperator = new ResourceOperator<TextProbe>(_ => new TextProbe("x"));

			AssetsManager.RegisterResourceOperator(resourceOperator);

			Assert.True(AssetsManager.IsRegistered<TextProbe>());
			Assert.False(AssetsManager.IsRegistered<OtherTextProbe>());
			Assert.Contains(typeof(TextProbe), AssetsManager.RegisteredTypes);
			Assert.Throws<ArgumentException>(() => AssetsManager.RegisterResourceOperator(resourceOperator));
			Assert.Throws<ArgumentNullException>(() => AssetsManager.RegisterResourceOperator<TextProbe>(null!));
		}
		finally
		{
			AssetsManager.Clear();
		}
	}

	[Fact]
	public void UnregisterResourceOperator_RemovesRegistration()
	{
		AssetsManager.Clear();
		try
		{
			AssetsManager.RegisterResourceOperator(new ResourceOperator<TextProbe>(_ => new TextProbe("x")));

			Assert.True(AssetsManager.UnregisterResourceOperator<TextProbe>());
			Assert.False(AssetsManager.UnregisterResourceOperator<TextProbe>());
			Assert.False(AssetsManager.IsRegistered<TextProbe>());
			Assert.Empty(AssetsManager.RegisteredTypes);
		}
		finally
		{
			AssetsManager.Clear();
		}
	}

	[Fact]
	public void Clear_RemovesEveryRegisteredOperator()
	{
		AssetsManager.Clear();
		try
		{
			AssetsManager.RegisterResourceOperator(new ResourceOperator<TextProbe>(_ => new TextProbe("x")));
			AssetsManager.RegisterResourceOperator(new ResourceOperator<OtherTextProbe>(_ => new OtherTextProbe("y")));
			Assert.Equal(2, AssetsManager.RegisteredTypes.Count);

			AssetsManager.Clear();

			Assert.Empty(AssetsManager.RegisteredTypes);
			Assert.False(AssetsManager.IsRegistered<TextProbe>());
			Assert.False(AssetsManager.IsRegistered<OtherTextProbe>());
		}
		finally
		{
			AssetsManager.Clear();
		}
	}

	[Fact]
	public void LoadResource_UsesRegisteredLoader()
	{
		using var scope = new HeadScope(new MemoryFileSystem().Add("probe.txt", "payload"));
		AssetsManager.Clear();
		try
		{
			AssetsManager.RegisterResourceOperator(new ResourceOperator<TextProbe>(stream => new TextProbe(Read(stream))));

			Assert.Equal("payload", AssetsManager.LoadResource<TextProbe>(scope["probe.txt"]).Text);
		}
		finally
		{
			AssetsManager.Clear();
		}
	}

	[Fact]
	public void LoadResource_WithoutOperator_Throws()
	{
		AssetsManager.Clear();

		var exception = Assert.Throws<InvalidOperationException>(() => AssetsManager.LoadResource<TextProbe>("unused://a.txt"));

		Assert.Contains("no resource operator", exception.Message);
	}

	[Fact]
	public void LoadResource_MissingFile_ThrowsFileNotFound()
	{
		using var scope = new HeadScope();
		AssetsManager.Clear();
		try
		{
			AssetsManager.RegisterResourceOperator(new ResourceOperator<TextProbe>(stream => new TextProbe(Read(stream))));

			Assert.Throws<FileNotFoundException>(() => AssetsManager.LoadResource<TextProbe>(scope["missing.txt"]));
		}
		finally
		{
			AssetsManager.Clear();
		}
	}

	[Fact]
	public async Task LoadResourceAsync_PrefersAsyncLoader()
	{
		using var scope = new HeadScope(new MemoryFileSystem().Add("probe.txt", "payload"));
		AssetsManager.Clear();
		try
		{
			AssetsManager.RegisterResourceOperator(new ResourceOperator<TextProbe>(
				stream => new TextProbe("sync"),
				AsyncLoader: (stream, cancellationToken) => new ValueTask<TextProbe>(new TextProbe("async"))));

			Assert.Equal("async", (await AssetsManager.LoadResourceAsync<TextProbe>(
				scope["probe.txt"], TestContext.Current.CancellationToken)).Text);
		}
		finally
		{
			AssetsManager.Clear();
		}
	}

	[Fact]
	public async Task LoadResourceAsync_FallsBackToSynchronousLoader()
	{
		using var scope = new HeadScope(new MemoryFileSystem().Add("probe.txt", "payload"));
		AssetsManager.Clear();
		try
		{
			AssetsManager.RegisterResourceOperator(new ResourceOperator<TextProbe>(stream => new TextProbe("sync")));

			Assert.Equal("sync", (await AssetsManager.LoadResourceAsync<TextProbe>(
				scope["probe.txt"], TestContext.Current.CancellationToken)).Text);
		}
		finally
		{
			AssetsManager.Clear();
		}
	}

	[Fact]
	public void SaveResource_UsesRegisteredSaver()
	{
		using var scope = new HeadScope();
		AssetsManager.Clear();
		try
		{
			AssetsManager.RegisterResourceOperator(new ResourceOperator<TextProbe>(
				stream => new TextProbe(Read(stream)),
				(stream, probe) => Write(stream, probe.Text)));

			AssetsManager.SaveResource(new TextProbe("saved"), scope["out.txt"]);

			Assert.Equal("saved", FileIO.ReadAllText(scope["out.txt"]));
		}
		finally
		{
			AssetsManager.Clear();
		}
	}

	[Fact]
	public void SaveResource_WithoutSaver_ThrowsNotSupported()
	{
		using var scope = new HeadScope();
		AssetsManager.Clear();
		try
		{
			AssetsManager.RegisterResourceOperator(new ResourceOperator<TextProbe>(stream => new TextProbe("x")));

			var exception = Assert.Throws<NotSupportedException>(
				() => AssetsManager.SaveResource(new TextProbe("x"), scope["out.txt"]));

			Assert.Contains("does not support saving", exception.Message);
			Assert.False(FileIO.FileExists(scope["out.txt"]));
		}
		finally
		{
			AssetsManager.Clear();
		}
	}

	[Fact]
	public async Task SaveResourceAsync_UsesAsyncSaver()
	{
		using var scope = new HeadScope();
		AssetsManager.Clear();
		try
		{
			AssetsManager.RegisterResourceOperator(new ResourceOperator<TextProbe>(
				stream => new TextProbe(Read(stream)),
				AsyncSaver: (stream, probe, cancellationToken) =>
				{
					Write(stream, probe.Text + "!");
					return ValueTask.CompletedTask;
				}));

			await AssetsManager.SaveResourceAsync(
				new TextProbe("async"), scope["out.txt"], TestContext.Current.CancellationToken);

			Assert.Equal("async!", FileIO.ReadAllText(scope["out.txt"]));
		}
		finally
		{
			AssetsManager.Clear();
		}
	}

	[Fact]
	public async Task SaveResourceAsync_FallsBackToSynchronousSaver()
	{
		using var scope = new HeadScope();
		AssetsManager.Clear();
		try
		{
			AssetsManager.RegisterResourceOperator(new ResourceOperator<TextProbe>(
				stream => new TextProbe(Read(stream)),
				(stream, probe) => Write(stream, probe.Text)));

			await AssetsManager.SaveResourceAsync(
				new TextProbe("sync-save"), scope["out.txt"], TestContext.Current.CancellationToken);

			Assert.Equal("sync-save", FileIO.ReadAllText(scope["out.txt"]));
		}
		finally
		{
			AssetsManager.Clear();
		}
	}

	[Fact]
	public async Task SaveResourceAsync_WithoutAnySaver_ThrowsNotSupported()
	{
		using var scope = new HeadScope();
		AssetsManager.Clear();
		try
		{
			AssetsManager.RegisterResourceOperator(new ResourceOperator<TextProbe>(stream => new TextProbe("x")));

			await Assert.ThrowsAsync<NotSupportedException>(
				async () => await AssetsManager.SaveResourceAsync(
					new TextProbe("x"), scope["out.txt"], TestContext.Current.CancellationToken));
			Assert.False(FileIO.FileExists(scope["out.txt"]));
		}
		finally
		{
			AssetsManager.Clear();
		}
	}

	private static string Read(Stream stream)
	{
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}

	private static void Write(Stream stream, string text)
	{
		using var writer = new StreamWriter(stream);
		writer.Write(text);
	}
}
