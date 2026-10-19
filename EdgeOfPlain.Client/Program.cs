using System;
using System.IO;
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
	private GameWindow? _window;
	private UiFonts? _fonts;

	protected override void InitResourceOperators()
	{
		base.InitResourceOperators();
		AssetsManager.RegisterResourceOperator(ImageOperatorBuilder.Build);
		AssetsManager.RegisterResourceOperator(TextureOperatorBuilder.Build);
		AssetsManager.RegisterResourceOperator(ShaderOperatorBuilder.Build);
		AssetsManager.RegisterResourceOperator(FontOperatorBuilder.Build);
	}

	private static void Main()
	{
		using var launcher = new Program();
		using var window = new GameWindow(1280, 720, "Edge of Plain");

		SfmlInput.Attach(window, launcher.Input);
		launcher.Init();
		using var fonts = new UiFonts();
		launcher._window = window;
		launcher._fonts = fonts;
		launcher.RunWithEventPump(window);
	}

	public void RunWithEventPump(IGameWindow window) => LaunchWithEventPump(window);

	protected override IRenderPipeline CreatePipeline(IGameWindow window)
		=> new SpriteBatch(((GameWindow)window).Target, () => window.Camera);

	protected override IScene CreateInitialScene()
		=> new LoadingScene(
			LoadContent,
			static () => new TestScene(),
			_fonts ?? throw new InvalidOperationException("UI fonts were not initialised."),
			_window?.Target ?? throw new InvalidOperationException("The window was not initialised."),
			TimeSpan.FromSeconds(1));

	protected override FileSystem BaseAssetsSystem => CoreAssets.Content;
	protected override string DataDirectory
		=> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "EdgeOfPlain");
	protected override string ModDirectory => Path.Combine(AppContext.BaseDirectory, "Mods");
}
