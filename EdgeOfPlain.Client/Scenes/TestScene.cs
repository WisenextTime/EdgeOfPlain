using System;
using System.Numerics;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Input;
using EdgeOfPlain.Core.Framework.Scenes;
using EdgeOfPlain.Core.Framework.Time;

namespace EdgeOfPlain.Client.Scenes;

public sealed class TestScene : SpriteScene
{
	private readonly RenderMetadata _ground = new()
	{
		Layer = 1,
		Color = new ColorF(40, 72, 40, 255),
		Size = new Vector2(1280, 180),
	};

	private readonly RenderMetadata _block = new()
	{
		Layer = 2,
		Color = new ColorF(120, 70, 40, 255),
		Size = new Vector2(48, 48),
	};

	private readonly RenderMetadata _unit = new()
	{
		Layer = 3,
		Size = new Vector2(96, 96),
		Origin = new Vector2(8, 8),
	};

	private readonly RenderMetadata _hud = new()
	{
		Layer = 100,
		Color = new ColorF(230, 230, 60, 255),
		Size = new Vector2(32, 32),
	};

	private SceneContext? _context;

	private Vector2 _previous = new(300, 150);
	private Vector2 _current = new(300, 150);

	private Camera2D _previousCamera = new(new Vector2(300, 150));
	private Camera2D _currentCamera = new(new Vector2(300, 150));

	public override void Enter(SceneContext context) => _context = context;

	public override void Update(LogicTime time, IInputState input)
	{
		_previous = _current;
		_current = new Vector2(300f + MathF.Sin((float)time.TotalSeconds * 1.5f) * 120f, 150f);

		_previousCamera = _currentCamera;

		var seconds = (float)time.TotalSeconds;
		_currentCamera = new Camera2D(
			_current,
			1.5f + MathF.Sin(seconds * 0.4f) * 0.3f,
			MathF.Sin(seconds * 0.2f) * 0.15f);
	}

	public override ISpriteBatch SpriteBatch => GameContext.Sprites;

	protected override void Draw(ISpriteBatch sprites, RenderTime time)
	{
		var camera = Camera2D.Interpolate(_previousCamera, _currentCamera, time.Alpha);
		var position = Vector2.Lerp(_previous, _current, time.Alpha);

		if (_context is { } context) context.Window.Camera = camera;

		sprites.Draw(_ground, 0, 0);
		sprites.Draw(_block, 200, 200);
		sprites.Draw(_unit, position.X, position.Y);

		_context?.Window.ResetCamera();
		sprites.Draw(_hud, 24, 24);
	}
}
