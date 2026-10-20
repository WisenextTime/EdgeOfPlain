using System;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Client.Resource;
using EdgeOfPlain.Core.Framework.Graphics;
using SFML.Graphics;

namespace EdgeOfPlain.Client;

public static class GameContext
{
	public static IGameWindow Window { get; private set; } = null!;

	public static UiFonts Fonts { get; private set; } = null!;

	public static IRenderTarget Target { get; private set; } = null!;

	public static SpriteBatch Sprites { get; private set; } = null!;

	public static void Initialize(IGameWindow window, UiFonts fonts, IRenderTarget target)
	{
		Window = window ?? throw new ArgumentNullException(nameof(window));
		Fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
		Target = target ?? throw new ArgumentNullException(nameof(target));

		Sprites = new SpriteBatch(target, fonts, () => window.Camera);
	}
}