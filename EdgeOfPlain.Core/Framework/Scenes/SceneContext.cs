using System;
using EdgeOfPlain.Core.Framework.Graphics;

namespace EdgeOfPlain.Core.Framework.Scenes;

public sealed class SceneContext
{
	public SceneContext(IGameWindow window, SceneManager scenes)
	{
		ArgumentNullException.ThrowIfNull(window);
		ArgumentNullException.ThrowIfNull(scenes);

		Window = window;
		Scenes = scenes;
	}

	public IGameWindow Window { get; }

	public SceneManager Scenes { get; }
}