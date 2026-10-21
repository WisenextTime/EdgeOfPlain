using System.Drawing;
using System.Numerics;
using EdgeOfPlain.Client.Resource.Textures;
using Xunit;

namespace EdgeOfPlain.Client.Tests;

public sealed class TextureDataTests
{
	[Fact]
	public void MakeKeyJoinsTexturePathAndSpriteId()
	{
		Assert.Equal("assets://base/Tiles/a.png#main", TextureData.MakeKey("assets://base/Tiles/a.png", "main"));
	}

	[Fact]
	public void ExposesSourceUvAndSize()
	{
		var data = new TextureData(
			"main",
			"assets://base/Tiles/a.png",
			new Rectangle(4, 8, 16, 32),
			new RectangleF(0.25f, 0.5f, 0.5f, 0.75f));

		Assert.Equal("assets://base/Tiles/a.png#main", data.Key);
		Assert.Equal("main", data.SpriteId);
		Assert.Equal("assets://base/Tiles/a.png", data.TexturePath);
		Assert.Equal(new Rectangle(4, 8, 16, 32), data.Source);
		Assert.Equal(new RectangleF(0.25f, 0.5f, 0.5f, 0.75f), data.Uv);
		Assert.Equal(new Vector2(16, 32), data.Size);
	}
}
