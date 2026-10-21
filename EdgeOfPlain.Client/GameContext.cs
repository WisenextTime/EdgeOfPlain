using System;
using System.Numerics;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Core.Framework.Graphics;
using SFML.Graphics;

namespace EdgeOfPlain.Client;

public static class GameContext
{
	public static IGameWindow Window { get; private set; } = null!;

	public static UiFonts Fonts { get; private set; } = null!;

	public static IRenderTarget RenderTarget { get; private set; } = null!;

	public static Camera2DState WorldCamera { get; private set; } = null!;

	public static void Initialize(IGameWindow window, UiFonts fonts, IRenderTarget target)
	{
		Window = window ?? throw new ArgumentNullException(nameof(window));
		Fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
		RenderTarget = target ?? throw new ArgumentNullException(nameof(target));
		WorldCamera = new Camera2DState(window.Camera);
	}
}
