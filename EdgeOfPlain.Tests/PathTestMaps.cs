using System;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Game;

namespace EdgeOfPlain.Tests;

internal static class PathTestMaps
{
	public const int GroundKind = 0;
	public const int WaterKind = 1;
	public const int CliffKind = 2;
	public const int PondKind = 3;
	public const int MudKind = 4;

	public static void RegisterTerrains()
	{
		if (ContentStore.Get<TerrainDef>("ground") is not null) return;

		ContentStore.Add(new TerrainDef("ground")
		{
			SourceMod = TestMods.Base,
			Passage = PassageMask.Ground | PassageMask.Air,
			MoveCost = 1,
		});
		ContentStore.Add(new TerrainDef("water")
		{
			SourceMod = TestMods.Base,
			Passage = PassageMask.Water | PassageMask.Air,
			MoveCost = 3,
		});
		ContentStore.Add(new TerrainDef("cliff")
		{
			SourceMod = TestMods.Base,
			Passage = PassageMask.Cliff,
			MoveCost = 0,
		});
		ContentStore.Add(new TerrainDef("pond")
		{
			SourceMod = TestMods.Base,
			Passage = PassageMask.Water,
			MoveCost = 3,
		});
		ContentStore.Add(new TerrainDef("mud")
		{
			SourceMod = TestMods.Base,
			Passage = PassageMask.Ground,
			MoveCost = 4,
		});
	}

	public static GameMap Build(int width, int height, Func<int, int, int> kind)
	{
		RegisterTerrains();

		if (ContentStore.Get<TilesetDef>("pathTiles") is null)
		{
			var tileset = new TilesetDef("pathTiles")
			{
				SourceMod = TestMods.Base,
				Width = 16,
				Height = 80,
				TileWidth = 16,
				TileHeight = 16,
			};
			tileset.TileData["0"] = new TileData { Terrain = "ground" };
			tileset.TileData["1"] = new TileData { Terrain = "water" };
			tileset.TileData["2"] = new TileData { Terrain = "cliff" };
			tileset.TileData["3"] = new TileData { Terrain = "pond" };
			tileset.TileData["4"] = new TileData { Terrain = "mud" };
			ContentStore.Add(tileset);
		}

		var map = new Map(width, height);
		map.Tilesets.Add(new TileSetData { TileSet = "pathTiles", FirstGid = 1 });

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var tileKind = kind(x, y);
				if (tileKind < 0 || tileKind > 4) tileKind = GroundKind;
				map.Tiles[x, y] = (ushort)(1 + tileKind);
			}
		}

		return GameMap.Build(map);
	}
}
