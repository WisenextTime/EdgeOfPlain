using System;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.World;

namespace EdgeOfPlain.Client.Rendering;

public class MapRenderPipeline(Func<Camera2D>? camera) : IRenderPipeline
{
	public void Begin()
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