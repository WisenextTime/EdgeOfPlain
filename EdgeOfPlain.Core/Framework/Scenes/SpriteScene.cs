using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Time;

namespace EdgeOfPlain.Core.Framework.Scenes;

public abstract class SpriteScene : Scene
{
	public override sealed void Draw(IRenderPipeline pipeline, RenderTime time)
	{
		if (pipeline is ISpriteBatch sprites) Draw(sprites, time);
	}

	protected abstract void Draw(ISpriteBatch sprites, RenderTime time);
}
