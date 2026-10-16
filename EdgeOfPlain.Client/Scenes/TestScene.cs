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

	private Vector2 _previous = new(300, 150);
	private Vector2 _current = new(300, 150);

	public override void Update(LogicTime time, IInputState input)
	{
		_previous = _current;
		_current = new Vector2(300f + MathF.Sin((float)time.TotalSeconds * 1.5f) * 120f, 150f);
	}

	protected override void Draw(ISpriteBatch sprites, RenderTime time)
	{
		var position = Vector2.Lerp(_previous, _current, time.Alpha);

		sprites.Draw(_ground, 0, 0);
		sprites.Draw(_block, 200, 200);
		sprites.Draw(_unit, position.X, position.Y);
	}
}
