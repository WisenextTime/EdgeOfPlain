using System;
using System.IO;
using System.Numerics;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Resource;
using SFML.Graphics;
using SFML.System;

namespace EdgeOfPlain.Client;

public static class Program
{
	private static void Main()
	{
		ResourceManager.RegisterResourceOperator(ImageOperatorBuilder.Build);
		ResourceManager.RegisterResourceOperator(TextureOperatorBuilder.Build);

		var assets = Path.Combine(AppContext.BaseDirectory, "assets");
		Directory.CreateDirectory(assets);
		EnsureTestTexture(Path.Combine(assets, "tile.png"));
		FileIO.RegisterFileSystem("assets", new PhysicalFileSystem(assets));

		using var window = new GameWindow(1280, 720, "Edge of Plain");
		Console.WriteLine($"size {window.Size}  viewport {window.Viewport}");

		var terrainPipeline = new TestRenderPipeline(window.Target);
		var entityPipeline = new TestRenderPipeline(window.Target);

		var ground = new RenderMetadata { Layer = 1, Color = new ColorF(40, 72, 40, 255), Size = new Vector2(1280, 180) };
		var block = new RenderMetadata { Layer = 2, Color = new ColorF(120, 70, 40, 255), Size = new Vector2(48, 48) };
		var unit = new RenderMetadata
		{
			Layer = 3,
			Texture = "assets://tile.png",
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

	private static void EnsureTestTexture(string path)
	{
		if (File.Exists(path)) return;

		using var image = new Image(new Vector2u(16, 16), new Color(220, 90, 60));
		using var stripe = new Image(new Vector2u(16, 6), new Color(70, 140, 220));
		image.Copy(stripe, new Vector2u(0, 5));

		if (!image.SaveToFile(path)) throw new IOException($"could not write '{path}'");
	}
}
