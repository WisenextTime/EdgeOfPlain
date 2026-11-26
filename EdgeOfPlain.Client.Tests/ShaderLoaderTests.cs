using System;
using System.IO;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Client.Resource.Shaders;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Resource;
using Xunit;
using Zio;
using Zio.FileSystems;

namespace EdgeOfPlain.Client.Tests
{
	public sealed class ShaderLoaderTests : IDisposable
	{
		private readonly TempDirectory _root = new();

		public ShaderLoaderTests()
		{
			ClientAssetsManager.Dispose();
			AssetsManager.Clear();
			FileIO.Reset();
			FileIO.Initialize(Path.Combine(_root.Path, "data"), Path.Combine(_root.Path, "mods"));

			_root.Write("Shaders/probe.shader", "#FRAG\nvoid main() { }");
		}

		public void Dispose()
		{
			ClientAssetsManager.Dispose();
			AssetsManager.Clear();
			FileIO.Reset();
			_root.Dispose();
		}

		[Fact]
		public void LoadAllCollectsShaderFilesWithoutCompilingThem()
		{
			var assets = new ModFileSystem();
			assets.RegisterNamespace("probe", Mount(_root.Path));
			FileIO.Mount(FileIO.Assets, assets);

			Assert.True(ShaderLoader.Register());

			var mod = new ProbeMod(new ModInfo("probe", new Version(1, 0, 0)));
			ModResourceLoader.LoadAll([mod]);

			Assert.Equal(1, ShaderLoader.PendingCount);
		}

		private static IFileSystem Mount(string directory)
		{
			var physical = new PhysicalFileSystem();
			return physical.GetOrCreateSubFileSystem(physical.ConvertPathFromInternal(Path.GetFullPath(directory)));
		}

		private sealed class ProbeMod(ModInfo info) : Mod
		{
			public override ModInfo ModInfo => info;
		}
	}
}