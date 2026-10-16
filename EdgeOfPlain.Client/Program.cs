using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Core;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Resource;

namespace EdgeOfPlain.Client;

public class Program : Launcher
{
	protected override void InitResourceOperators()
	{
		base.InitResourceOperators();
		AssetsManager.RegisterResourceOperator(ImageOperatorBuilder.Build);
		AssetsManager.RegisterResourceOperator(TextureOperatorBuilder.Build);
	}

	private static void Main()
	{
		var launcher = new Program();
		launcher.Init();
		
		using var window = new GameWindow(1280, 720, "Edge of Plain");
		var terrainPipeline = new TestRenderPipeline(window.Target);
		var entityPipeline = new TestRenderPipeline(window.Target);

		var ground = new RenderMetadata { Layer = 1, Color = new ColorF(40, 72, 40, 255), Size = new Vector2(1280, 180) };
		var block = new RenderMetadata { Layer = 2, Color = new ColorF(120, 70, 40, 255), Size = new Vector2(48, 48) };
		var unit = new RenderMetadata
		{
			Texture = "assets://base/Textures/Tiles/TestTile.png",
			Layer = 3,
			Size = new Vector2(96, 96),
			Origin = new Vector2(8, 8),
			Rotation = 0.35f,
		};

		while (window.IsOpen)
		{
			window.PollEvents();

			window.Clear(new ColorF(16, 16, 24, 255));

			terrainPipeline.Begin();
			terrainPipeline.Draw(ground, 0, 0);
			terrainPipeline.Draw(block, 200, 200);
			terrainPipeline.End();

			entityPipeline.Begin();
			entityPipeline.Draw(unit, 300, 150);
			entityPipeline.End();

			window.Present();          
		}
	}

	protected override FileSystem BaseAssetsSystem => new AssemblyResourceSystem(Assembly.GetAssembly(typeof(Launcher))!);
	protected override string DataDirectory
		=> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "EdgeOfPlain");
	protected override string ModDirectory => Path.Combine(AppContext.BaseDirectory, "Mods");
}
