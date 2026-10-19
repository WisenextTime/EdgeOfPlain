using EdgeOfPlain.Core.Framework.Resource;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class CoreAssetsTests
{
	[Fact]
	public void BootstrapFolderIsHiddenFromTheContentPipeline()
	{
		Assert.False(CoreAssets.Content.DirectoryExists("Misc"));
		Assert.False(CoreAssets.Content.FileExists("Misc/Fonts/SourceHanSansSC-Bold.otf"));
		Assert.False(CoreAssets.Content.FileExists("Misc/Fonts/SourceHanSans-Light.otf"));
	}

	[Fact]
	public void BootstrapFolderIsAvailableOutsideTheContentPipeline()
	{
		Assert.True(CoreAssets.Bootstrap.DirectoryExists("Misc/Fonts"));
		Assert.True(CoreAssets.Bootstrap.FileExists("Misc/Fonts/SourceHanSansSC-Bold.otf"));
		Assert.True(CoreAssets.Bootstrap.FileExists("Misc/Fonts/SourceHanSans-Light.otf"));
	}

	[Fact]
	public void ContentAssetsAreUnaffected()
	{
		Assert.True(CoreAssets.Content.FileExists("Contents/Tilesets/TestTile.toml"));
		Assert.False(CoreAssets.Bootstrap.FileExists("Contents/Tilesets/TestTile.toml"));
	}

	[Fact]
	public void EngineImportSidecarsAreNotEmbedded()
	{
		Assert.False(CoreAssets.Bootstrap.FileExists("Misc/Fonts/SourceHanSansSC-Bold.otf.import"));
	}
}
