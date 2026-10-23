using System;
using System.IO;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Client.Resource.Shaders;
using EdgeOfPlain.Client.Resource.Textures;
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
		AssetsManager.RegisterResourceOperator(ShaderOperatorBuilder.Build);
		AssetsManager.RegisterResourceOperator(FontOperatorBuilder.Build);

		TextureLoader.Register();
		ShaderLoader.Register();
	}

	private static void Main()
	{
		var launcher = new Program();
		using var window = new GameWindow(1280, 720, "Edge of Plain");

		SfmlInput.Attach(window, launcher.Input);

		launcher.Init();

		using var fonts = new UiFonts();
		GameContext.Initialize(window, fonts, window.Target);

		try
		{
			launcher.RunWithEventPump(window);
		}
		finally
		{
			ClientAssetsManager.Dispose();
		}
	}

	public void RunWithEventPump(IGameWindow window) => LaunchWithEventPump(window);

	protected override IScene CreateInitialScene()
		=> new LoadingScene(
			LoadContent,
			static () => new TestMapScene(),
			GameContext.Fonts,
			GameContext.RenderTarget,
			TimeSpan.FromSeconds(1));

	protected override FileSystem BaseAssetsSystem => CoreAssets.Content;
	protected override string DataDirectory
		=> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "EdgeOfPlain");
	protected override string ModDirectory => Path.Combine(AppContext.BaseDirectory, "Mods");
}