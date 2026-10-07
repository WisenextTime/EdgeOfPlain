namespace EdgeOfPlain.Core.Framework.Graphics;

public interface IRenderPipeline
{
	void Begin();

	void Draw(RenderMetadata metadata, float x, float y);

	void End();
}