using System;
using EdgeOfPlain.Core.Framework.Events;
using EdgeOfPlain.Core.Framework.Graphics;

namespace EdgeOfPlain.Core.Framework.Scenes;

public sealed class SceneContext
{
	public SceneContext(IGameWindow window, SceneManager scenes, EventBus events)
	{
		ArgumentNullException.ThrowIfNull(window);
		ArgumentNullException.ThrowIfNull(scenes);
		ArgumentNullException.ThrowIfNull(events);

		Window = window;
		Scenes = scenes;
		Events = events;
	}

	public IGameWindow Window { get; }

	public SceneManager Scenes { get; }

	public EventBus Events { get; }
}
