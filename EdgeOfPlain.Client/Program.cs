using System;
using System.IO;
using System.Reflection;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Client.Scenes;
using EdgeOfPlain.Core;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.IO;
using EdgeOfPlain.Core.Framework.Resource;
using EdgeOfPlain.Core.Framework.Scenes;

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
		using var launcher = new Program();
		launcher.Init();

		using var window = new GameWindow(1280, 720, "Edge of Plain");

		SfmlInput.Attach(window, launcher.Input);

		launcher.RunWithEventPump(window);
	}

	public void RunWithEventPump(IGameWindow window) => LaunchWithEventPump(window);

	protected override IRenderPipeline CreatePipeline(IGameWindow window)
		=> new TestRenderPipeline(((GameWindow)window).Target, () => window.Camera);

	protected override IScene CreateInitialScene() => new TestScene();

	protected override FileSystem BaseAssetsSystem => new AssemblyResourceSystem(Assembly.GetAssembly(typeof(Launcher))!);
	protected override string DataDirectory
		=> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "EdgeOfPlain");
	protected override string ModDirectory => Path.Combine(AppContext.BaseDirectory, "Mods");
}
