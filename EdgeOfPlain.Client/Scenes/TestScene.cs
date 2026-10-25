using System;
using System.Numerics;
using EdgeOfPlain.Client.Rendering;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;
using EdgeOfPlain.Core.Framework.Scenes;
using EdgeOfPlain.Core.Framework.Time;

namespace EdgeOfPlain.Client.Scenes;

public sealed class TestScene : Scene
{
	private readonly RenderMetadata _ground = new()
	{
		Layer = 1,
		Color = new ColorF(40, 72, 40, 255),
		Size = new Vector2(1280, 180),
	};

	private readonly RenderMetadata _block = new()
	{
		Texture = "assets://base/Textures/Tiles/testTile#main",
		Layer = 3,
		Color = new ColorF(120, 70, 40, 255),
	};

	private readonly RenderMetadata _unit = new()
	{
		Layer = 2,
		Size = new Vector2(96, 96),
		Origin = new Vector2(8, 8),
	};

	private readonly RenderMetadata _hud = new()
	{
		Layer = 100,
		Color = new ColorF(230, 230, 60, 255),
		Size = new Vector2(32, 32),
	};

	private SpriteBatch? _worldSprites;
	private SpriteBatch? _hudSprites;

	private Vector2 _previous = new(300, 150);
	private Vector2 _current = new(300, 150);

	public override void Enter(SceneContext context)
	{
		GameContext.WorldCamera.Reset(new Camera2D(new Vector2(300, 150)));

		var target = GameContext.RenderTarget;
		_worldSprites = new SpriteBatch(target);
		_hudSprites = new SpriteBatch(target);
	}

	public override void Exit()
	{
		_hudSprites?.Dispose();
		_worldSprites?.Dispose();
		_hudSprites = null;
		_worldSprites = null;
	}

	public override void Update(LogicTime time, IInputState input)
	{
		_previous = _current;
		_current = new Vector2(300f + MathF.Sin((float)time.TotalSeconds * 1.5f) * 120f, 150f);

		var seconds = (float)time.TotalSeconds;
		GameContext.WorldCamera.Push(new Camera2D(
			_current,
			1.5f + MathF.Sin(seconds * 0.4f) * 0.3f,
			MathF.Sin(seconds * 0.2f) * 0.15f));
	}

	public override void Draw(RenderTime time)
	{
		var camera = GameContext.WorldCamera.Evaluate(time.Alpha);
		var position = Vector2.Lerp(_previous, _current, time.Alpha);

		GameContext.Window.Camera = camera;

		if (_worldSprites is null || _hudSprites is null) return;

		_worldSprites.Begin(camera);
		_worldSprites.Draw(_ground, 0, 0);
		_worldSprites.Draw(_block, 200, 200);
		_worldSprites.Draw(_unit, position.X, position.Y);
		_worldSprites.End();

		_hudSprites.Begin();
		_hudSprites.Draw(_hud, 24, 24);
		_hudSprites.End();
	}
}