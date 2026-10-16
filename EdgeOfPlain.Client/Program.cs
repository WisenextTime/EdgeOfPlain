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
		var launcher = new Program();
		launcher.Init();

		using var window = new GameWindow(1280, 720, "Edge of Plain");

		// 平台事件 → Core 输入状态；循环里每个渲染帧开始时结算一次。
		SfmlInput.Attach(window, launcher.Input);

		launcher.Launch(window);
	}

	protected override IRenderPipeline CreatePipeline(IGameWindow window)
		=> new TestRenderPipeline(((GameWindow)window).Target);

	protected override IScene CreateInitialScene() => new TestScene();

	protected override FileSystem BaseAssetsSystem => new AssemblyResourceSystem(Assembly.GetAssembly(typeof(Launcher))!);
	protected override string DataDirectory
		=> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "EdgeOfPlain");
	protected override string ModDirectory => Path.Combine(AppContext.BaseDirectory, "Mods");
}
