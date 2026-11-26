using System;
using System.Drawing;
using System.IO;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Client.Resource.Textures;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Resource;
using Xunit;
using Zio;
using Zio.FileSystems;

namespace EdgeOfPlain.Client.Tests
{
	public sealed class TextureLoaderTests : IDisposable
	{
		private const string Namespace = "probe";
		private static readonly UPath _directory = new($"/assets/{Namespace}/Textures/Tiles");

		private readonly TempDirectory _root = new();

		public TextureLoaderTests()
		{
			ClientAssetsManager.Dispose();
			AssetsManager.Clear();
			FileIO.Reset();
			FileIO.Initialize(Path.Combine(_root.Path, "data"), Path.Combine(_root.Path, "mods"));

			_root.Write("Textures/Tiles/Test.png", TestAssets.OnePixelPng());
			_root.Write("Textures/Tiles/Test.atlas", TestAssets.OnePixelAtlas);

			AssetsManager.RegisterResourceOperator(ImageOperatorBuilder.Build);
			AssetsManager.RegisterResourceOperator(TextureAtlasOperatorBuilder.Build);

			var assets = new ModFileSystem();
			assets.RegisterNamespace(Namespace, Mount(_root.Path));
			FileIO.Mount(FileIO.Assets, assets);
		}

		public void Dispose()
		{
			ClientAssetsManager.Dispose();
			AssetsManager.Clear();
			FileIO.Reset();
			_root.Dispose();
		}

		[Fact]
		public void LoadBuildsTextureDataFromPngAndAtlas()
		{
			TextureLoader.Load(CreateDirectory());

			var key = TextureData.MakeKey($"{_directory}/Test", "main");

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

			var path = $"{_directory}/Test.png";
			Assert.True(ClientAssetsManager.TryGet<SFML.Graphics.Image>(path, out var image));
			Assert.Equal(1u, image.Size.X);
			Assert.Equal(1u, image.Size.Y);
		}

		private static IFileSystem Mount(string directory)
		{
			var physical = new PhysicalFileSystem();
			return physical.GetOrCreateSubFileSystem(physical.ConvertPathFromInternal(Path.GetFullPath(directory)));
		}

		private static ModResourceDirectory CreateDirectory()
			=> new(
				_directory,
				new ModInfo(Namespace, new Version(1, 0, 0)),
				[
					$"{_directory}/Test.png",
					$"{_directory}/Test.atlas",
				]);
	}
}