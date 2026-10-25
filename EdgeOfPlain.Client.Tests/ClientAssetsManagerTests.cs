using System;
using System.Collections.Generic;
using System.IO;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Client.Resource.Textures;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Resource;
using Xunit;

namespace EdgeOfPlain.Client.Tests;

public sealed class ClientAssetsManagerTests : IDisposable
{
	private const string Head = "client-asset-tests";

	private readonly TempDirectory _root = new();
	private int _loadCount;

	private sealed record ProbeAsset(string Text);

	private sealed class DisposableAsset : IDisposable
	{
		public bool IsDisposed { get; private set; }

		public void Dispose() => IsDisposed = true;
	}

	public sealed record NullAsset;

	public ClientAssetsManagerTests()
	{
		ClientAssetsManager.Dispose();
		AssetsManager.Clear();
		FileIO.Clear();
		FileIO.RegisterFileSystem(Head, new PhysicalFileSystem(_root.Path));
	}

	public void Dispose()
	{
		ClientAssetsManager.Dispose();
		AssetsManager.Clear();
		FileIO.UnregisterFileSystem(Head);
		_root.Dispose();
	}

	[Fact]
	public void GetLoadsOnceAndCachesTheResult()
	{
		_root.Write("probe.txt", "hello");
		AssetsManager.RegisterResourceOperator(new ResourceOperator<ProbeAsset>(stream =>
		{
			_loadCount++;
			using var reader = new StreamReader(stream);
			return new ProbeAsset(reader.ReadToEnd());
		}));

		var first = ClientAssetsManager.Get<ProbeAsset>($"{Head}://probe.txt");
		var second = ClientAssetsManager.Get<ProbeAsset>($"{Head}://probe.txt");

		Assert.Same(first, second);
		Assert.Equal("hello", first.Text);
		Assert.Equal(1, _loadCount);
	}

	[Fact]
	public void TryGetReportsWhetherAnAssetIsLoaded()
	{
		_root.Write("probe.txt", "hello");
		AssetsManager.RegisterResourceOperator(new ResourceOperator<ProbeAsset>(
			stream => new ProbeAsset(new StreamReader(stream).ReadToEnd())));

		Assert.False(ClientAssetsManager.TryGet<ProbeAsset>($"{Head}://probe.txt", out _));

		ClientAssetsManager.Get<ProbeAsset>($"{Head}://probe.txt");

		Assert.True(ClientAssetsManager.TryGet<ProbeAsset>($"{Head}://probe.txt", out var asset));
		Assert.Equal("hello", asset.Text);
	}

	[Fact]
	public void GetAllReturnsOnlyTheRequestedType()
	{
		AssetsManager.RegisterResourceOperator(new ResourceOperator<ProbeAsset>(_ => new ProbeAsset("default")));
		_root.Write("a.txt", "a");
		_root.Write("b.txt", "b");

		ClientAssetsManager.Get<ProbeAsset>($"{Head}://a.txt");
		ClientAssetsManager.Get<ProbeAsset>($"{Head}://b.txt");

		Assert.Equal(2, ClientAssetsManager.GetAll<ProbeAsset>().Count);
	}

	[Fact]
	public void GetThrowsWhenNoOperatorIsRegistered()
	{
		Assert.Throws<KeyNotFoundException>(() => ClientAssetsManager.Get<TextureData>("assets://missing#main"));
	}

	[Fact]
	public void GetRejectsBlankKeys()
	{
		Assert.Throws<ArgumentException>(() => ClientAssetsManager.Get<ProbeAsset>(""));
		Assert.Throws<ArgumentException>(() => ClientAssetsManager.TryGet<ProbeAsset>("", out _));
	}

	[Fact]
	public void GetRejectsNullLoadedAssets()
	{
		_root.Write("null.txt", "x");
		AssetsManager.RegisterResourceOperator(new ResourceOperator<NullAsset>(_ => null!));

		Assert.Throws<InvalidOperationException>(() => ClientAssetsManager.Get<NullAsset>($"{Head}://null.txt"));
	}

	[Fact]
	public void DisposeDisposesStoredResourcesAndClearsTheStore()
	{
		_root.Write("disposable.txt", "x");
		AssetsManager.RegisterResourceOperator(new ResourceOperator<DisposableAsset>(_ => new DisposableAsset()));

		var asset = ClientAssetsManager.Get<DisposableAsset>($"{Head}://disposable.txt");

		ClientAssetsManager.Dispose();

		Assert.True(asset.IsDisposed);
		Assert.Empty(ClientAssetsManager.GetAll<DisposableAsset>());
	}
}
