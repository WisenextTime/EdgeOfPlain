namespace EdgeOfPlain.Core.Framework.Graphics;

public interface ISpriteBatch : IRenderPipeline
{
	void Draw(RenderMetadata metadata, float x, float y);
}
