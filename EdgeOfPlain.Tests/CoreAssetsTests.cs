using EdgeOfPlain.Core.Framework.Resource;
using Xunit;
using Zio;

namespace EdgeOfPlain.Tests
{
	public sealed class CoreAssetsTests
	{
		[Fact]
		public void BootstrapFolderIsHiddenFromTheContentPipeline()
		{
			Assert.False(CoreAssets.Content.DirectoryExists(new UPath("/Misc")));
			Assert.False(CoreAssets.Content.FileExists(new UPath("/Misc/Fonts/SourceHanSansSC-Bold.otf")));
			Assert.False(CoreAssets.Content.FileExists(new UPath("/Misc/Fonts/SourceHanSans-Light.otf")));
		}

		[Fact]
		public void BootstrapFolderIsAvailableOutsideTheContentPipeline()
		{
			Assert.True(CoreAssets.Bootstrap.DirectoryExists(new UPath("/Misc/Fonts")));
			Assert.True(CoreAssets.Bootstrap.FileExists(new UPath("/Misc/Fonts/SourceHanSansSC-Bold.otf")));
			Assert.True(CoreAssets.Bootstrap.FileExists(new UPath("/Misc/Fonts/SourceHanSans-Light.otf")));
		}

		[Fact]
		public void ContentAssetsAreUnaffected()
		{
			Assert.True(CoreAssets.Content.FileExists(new UPath("/Contents/Tilesets/testTile.toml")));
			Assert.False(CoreAssets.Bootstrap.FileExists(new UPath("/Contents/Tilesets/testTile.toml")));
		}

		[Fact]
		public void EngineImportSidecarsAreNotEmbedded()
		{
			Assert.False(CoreAssets.Bootstrap.FileExists(new UPath("/Misc/Fonts/SourceHanSansSC-Bold.otf.import")));
		}
	}
}