namespace EdgeOfPlain.Core.Framework.Graphics;

public interface ISpriteBatch : IRenderPipeline
{
	void Draw(RenderMetadata metadata, float x, float y);
	void DrawText(string text, int size, float x, float y, uint layer);
}