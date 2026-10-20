using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;

namespace EdgeOfPlain.Core.Content;

internal static class Terrains
{
	public static TerrainDef Ground { get; } = new("ground")
	{
		Passage = TerrainDef.PassageMask.Air | TerrainDef.PassageMask.Ground,
	};

	public static TerrainDef Water { get; } = new("water")
	{
		Passage = TerrainDef.PassageMask.Water | TerrainDef.PassageMask.Air,
	};

	public static void RegisterAll(ModContext context)
	{
		context.AddContent(Ground);
		context.AddContent(Water);
	}
}