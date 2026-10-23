namespace EdgeOfPlain.Core.Framework.Graphics;

public interface IRenderPipeline
{
	void Begin(Camera2D? camera2D = null);

	void End();
}