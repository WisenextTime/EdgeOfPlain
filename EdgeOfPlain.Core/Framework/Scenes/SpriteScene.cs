using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Time;

namespace EdgeOfPlain.Core.Framework.Scenes;

public abstract class SpriteScene : Scene
{
	public abstract ISpriteBatch SpriteBatch { get; }
	public override sealed void Draw(RenderTime time)
	{
		var sprites = SpriteBatch;

		sprites.Begin();
		Draw(sprites, time);
		sprites.End();
	}

	protected abstract void Draw(ISpriteBatch sprites, RenderTime time);
}
