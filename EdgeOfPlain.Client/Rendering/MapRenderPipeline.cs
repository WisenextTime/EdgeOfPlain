using EdgeOfPlain.Core.Framework.Graphics;

namespace EdgeOfPlain.Client.Rendering;

public class MapRenderPipeline : IRenderPipeline
{
	public void Begin()
	{
		
	}

	public void Draw(RenderMetadata metadata, float x, float y)
	{
		// TODO: 地图专用管线尚未实现，现在和 Begin/End 一样是空壳。
	}

	public void End()
	{
		
	}
}
