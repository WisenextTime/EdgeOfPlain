using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.World;
using SFML.Graphics;

namespace EdgeOfPlain.Client.Rendering;

public class MapRenderPipeline : IRenderPipeline
{
	public void Begin(Camera2D? camera2D)
	{
		
	}

	public void End()
	{
		
	}

	public void ChangeMap(Map map)
	{
		
	}
}

public class TileRenderer(TileRendererDef def)
{
	public TileRendererDef Definition { get; } = def;
}