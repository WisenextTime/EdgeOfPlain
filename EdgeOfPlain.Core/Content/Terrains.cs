using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;

namespace EdgeOfPlain.Core.Content;

internal static class Terrains
{
	public static TerrainDef Ground => new("ground")
	{
		Passage = TerrainDef.PassageMask.Air | TerrainDef.PassageMask.Ground,
	};

	public static void RegisterAll(ModContext context)
	{
		context.AddContent(Ground);
	}
}
