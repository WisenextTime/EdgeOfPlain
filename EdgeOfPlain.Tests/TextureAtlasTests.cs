using EdgeOfPlain.Core.Framework.Resource;
using Tomlyn;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class TextureAtlasTests
{
	[Fact]
	public void ParsesSpritesFromSampleAsset()
	{
		var atlas = TextureAtlas.Parse(CoreAssets.Content.ReadAllText("Textures/Tiles/TestTile.atlas"));

		var sprite = Assert.Single(atlas.Sprites);
		Assert.Equal("main", sprite.Id);
		Assert.Equal(0, sprite.X);
		Assert.Equal(0, sprite.Y);
		Assert.Equal(128, sprite.Width);
		Assert.Equal(64, sprite.Height);
	}

	[Fact]
	public void RejectsAtlasWithoutSprites()
	{
		Assert.Throws<TomlException>(() => TextureAtlas.Parse(""));
	}
}
