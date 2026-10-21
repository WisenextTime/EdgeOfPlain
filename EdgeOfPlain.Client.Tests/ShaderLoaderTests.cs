using System;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Client.Resource.Shaders;
using EdgeOfPlain.Core;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Resource;
using Xunit;

namespace EdgeOfPlain.Client.Tests;

public sealed class ShaderLoaderTests : IDisposable
{
	private readonly TempDirectory _root = new();

	public ShaderLoaderTests()
	{
		ClientAssetsManager.Dispose();
		AssetsManager.Clear();
		FileIO.Clear();

		_root.Write("Shaders/probe.shader", "#FRAG\nvoid main() { }");
	}

	public void Dispose()
	{
		ClientAssetsManager.Dispose();
		AssetsManager.Clear();
		FileIO.Clear();
		_root.Dispose();
	}

	[Fact]
	public void LoadAllCollectsShaderFilesWithoutCompilingThem()
	{
		var assets = new ModFileSystem();
		assets.RegisterNamespace("probe", new PhysicalFileSystem(_root.Path));
		FileIO.RegisterFileSystem(Launcher.AssetsHead, assets);

		Assert.True(ShaderLoader.Register());

		var mod = new ProbeMod(new ModInfo("probe", new Version(1, 0, 0)));
		ModResourceLoader.LoadAll([mod]);

		Assert.Equal(1, ShaderLoader.PendingCount);
	}

	private sealed class ProbeMod(ModInfo info) : Mod
	{
		public override ModInfo ModInfo => info;
	}
}
