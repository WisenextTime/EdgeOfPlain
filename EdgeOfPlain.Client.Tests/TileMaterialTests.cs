using System.Drawing;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Client.Resource.Textures;
using Xunit;

namespace EdgeOfPlain.Client.Tests;

public sealed class TileMaterialTests
{
	[Fact]
	public void MapsLocalUvIntoTextureRegion()
	{
		var texture = new TextureData(
			"main",
			"assets://base/Textures/Tiles/testTile",
			new Rectangle(32, 16, 64, 32),
			new RectangleF(0.125f, 0.125f, 0.25f, 0.25f));
		var material = new TileMaterial(texture, new FakeRenderTarget());

		var uv = material.MapUv(new RectangleF(0.25f, 0.5f, 0.125f, 0.125f));

		Assert.Equal(0.1875f, uv.Left);
		Assert.Equal(0.25f, uv.Top);
		Assert.Equal(0.03125f, uv.Width);
		Assert.Equal(0.03125f, uv.Height);
	}
}
