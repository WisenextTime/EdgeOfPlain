using System;
using System.Drawing;
using System.IO;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Client.Resource.Textures;
using EdgeOfPlain.Core;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Resource;
using Xunit;

namespace EdgeOfPlain.Client.Tests;

public sealed class TextureLoaderTests : IDisposable
{
	private const string Head = Launcher.AssetsHead;
	private const string Namespace = "probe";

	private readonly TempDirectory _root = new();

	public TextureLoaderTests()
	{
		ClientAssetsManager.Dispose();
		AssetsManager.Clear();
		FileIO.Clear();

		_root.Write("Textures/Tiles/Test.png", TestAssets.OnePixelPng());
		_root.Write("Textures/Tiles/Test.atlas", TestAssets.OnePixelAtlas);

		AssetsManager.RegisterResourceOperator(ImageOperatorBuilder.Build);
		AssetsManager.RegisterResourceOperator(TextureAtlasOperatorBuilder.Build);

		var assets = new ModFileSystem();
		assets.RegisterNamespace(Namespace, new PhysicalFileSystem(_root.Path));
		FileIO.RegisterFileSystem(Head, assets);
	}

	public void Dispose()
	{
		ClientAssetsManager.Dispose();
		AssetsManager.Clear();
		FileIO.Clear();
		_root.Dispose();
	}

	[Fact]
	public void LoadBuildsTextureDataFromPngAndAtlas()
	{
		TextureLoader.Load(CreateDirectory());

		var key = TextureData.MakeKey($"assets://{Namespace}/Textures/Tiles/Test.png", "main");

		Assert.True(ClientAssetsManager.TryGet<TextureData>(key, out var data));
		Assert.Equal("main", data.SpriteId);
		Assert.Equal(new Rectangle(0, 0, 1, 1), data.Source);
		Assert.Equal(new RectangleF(0, 0, 1, 1), data.Uv);
		Assert.Single(ClientAssetsManager.Textures);
	}

	[Fact]
	public void LoadAlsoStoresTheSourceImage()
	{
		TextureLoader.Load(CreateDirectory());

		var path = $"assets://{Namespace}/Textures/Tiles/Test.png";
		Assert.True(ClientAssetsManager.TryGet<SFML.Graphics.Image>(path, out var image));
		Assert.Equal(1u, image.Size.X);
		Assert.Equal(1u, image.Size.Y);
	}

	private static ModResourceDirectory CreateDirectory()
		=> new(
			$"assets://{Namespace}/Textures/Tiles/",
			new ModInfo(Namespace, new Version(1, 0, 0)),
			[
				$"assets://{Namespace}/Textures/Tiles/Test.png",
				$"assets://{Namespace}/Textures/Tiles/Test.atlas",
			]);
}
