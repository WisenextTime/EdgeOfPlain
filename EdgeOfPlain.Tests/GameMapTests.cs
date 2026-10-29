using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Game;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class GameMapTests : IDisposable
{
	public GameMapTests()
	{
		ContentStore.Reset();
		ContentStore.Add(new TerrainDef("ground") { SourceMod = TestMods.Base, Passage = PassageMask.Ground, MoveCost = 1 });
	}

	public void Dispose() => ContentStore.Reset();

	[Fact]
	public void BuildResolvesTilesetUvAndTileData()
	{
		var tileset = RegisterTileset("terrain", 64, 32);
		tileset.TileData["1"] = new TileData { Terrain = "water" };
		RegisterTerrain("ground", PassageMask.Ground);
		RegisterTerrain("water", PassageMask.Water, 3);

		var map = new Map(3, 2);
		map.Tilesets.Add(new TileSetData { TileSet = "terrain", FirstGid = 7 });
		map.Tiles[0, 0] = 8;
		map.Tiles[1, 0] = 7;
		map.Tiles[2, 1] = 10;

		var gameMap = GameMap.Build(map);

		Assert.Equal(32, GameMap.TileSize);
		Assert.Equal(new RectangleF(0f, 0f, 96f, 64f), gameMap.Bounds);
		Assert.Single(gameMap.Tilesets);
		Assert.Single(gameMap.Chunks);

		var water = gameMap.GetTile(0, 0);
		Assert.Equal(8, water.Id);
		Assert.Same(tileset, water.Tileset?.Definition);
		Assert.Equal(1, water.LocalId);
		Assert.Equal(1, water.Column);
		Assert.Equal(0, water.Row);
		Assert.Equal(new RectangleF(16f / 64f, 0f, 16f / 64f, 16f / 32f), water.Uv);
		Assert.Equal("water", water.Terrain?.Id);

		var ground = gameMap.GetTile(1, 0);
		Assert.Equal(7, ground.Id);
		Assert.Equal("ground", ground.Terrain?.Id);
	}

	[Fact]
	public void BuildSplitsChunksAtMapEdges()
	{
		RegisterTileset("chunk", 16, 16);

		var map = new Map(65, 33);
		map.Tilesets.Add(new TileSetData { TileSet = "chunk", FirstGid = 1 });

		var gameMap = GameMap.Build(map);

		Assert.Equal(6, gameMap.Chunks.Count);

		var first = gameMap.Chunks[0];
		Assert.Equal(0, first.TileX);
		Assert.Equal(0, first.TileY);
		Assert.Equal(32, first.Columns);
		Assert.Equal(32, first.Rows);
		Assert.Equal(new RectangleF(0f, 0f, 1024f, 1024f), first.Bounds);

		var last = gameMap.Chunks[^1];
		Assert.Equal(64, last.TileX);
		Assert.Equal(32, last.TileY);
		Assert.Equal(1, last.Columns);
		Assert.Equal(1, last.Rows);
		Assert.Equal(new RectangleF(2048f, 1024f, 32f, 32f), last.Bounds);
	}

	[Fact]
	public void TileAndWorldCoordinatesRoundTrip()
	{
		RegisterTileset("coords", 16, 16);

		var map = new Map(4, 4);
		map.Tilesets.Add(new TileSetData { TileSet = "coords", FirstGid = 1 });

		var gameMap = GameMap.Build(map);

		Assert.Equal(new Vector2(64f, 96f), gameMap.TileToWorld(2, 3));
		Assert.Equal(new Vector2(80f, 112f), gameMap.TileCenter(2, 3));
		Assert.Equal(new Point(2, 3), gameMap.WorldToTile(new Vector2(65f, 97f)));
		Assert.Equal(new RectangleF(64f, 96f, 32f, 32f), gameMap.GetTileBounds(2, 3));
	}

	[Fact]
	public void PassabilityAndMoveCostUseResolvedTerrain()
	{
		var tileset = RegisterTileset("passability", 16, 16);
		tileset.TileData["0"] = new TileData { Terrain = "water" };
		RegisterTerrain("water", PassageMask.Water, 4);

		var map = new Map(2, 1);
		map.Tilesets.Add(new TileSetData { TileSet = "passability", FirstGid = 1 });
		map.Tiles[0, 0] = 1;

		var gameMap = GameMap.Build(map);

		Assert.True(gameMap.IsPassable(0, 0, PassageMask.Water));
		Assert.False(gameMap.IsPassable(0, 0, PassageMask.Ground));
		Assert.Equal(4, gameMap.GetMoveCost(0, 0, PassageMask.Water));
	
		Assert.False(gameMap.IsPassable(1, 0, PassageMask.Water));
		Assert.Equal(0, gameMap.GetMoveCost(1, 0, PassageMask.Water));
	}

	[Fact]
	public void CollectsDistinctRenderersFromTileData()
	{
		var tileset = RegisterTileset("renderers", 64, 64);
		tileset.TileData["0"] = new TileData { Renderer = "grass" };
		tileset.TileData["1"] = new TileData { Renderer = "water" };
		ContentStore.Add(new TileRendererDef("grass") { SourceMod = TestMods.Base, ShaderPath = "shaders/grass" });
		ContentStore.Add(new TileRendererDef("water") { SourceMod = TestMods.Base, ShaderPath = "shaders/water" });

		var map = new Map(1, 1);
		map.Tilesets.Add(new TileSetData { TileSet = "renderers", FirstGid = 1 });
		map.Tiles[0, 0] = 1;

		var gameMap = GameMap.Build(map);

		Assert.Equal(2, gameMap.Renderers.Count);
		Assert.Equal("grass", gameMap.Renderers[0].Id);
		Assert.Equal("water", gameMap.Renderers[1].Id);
	}

	[Fact]
	public void MissingTileRendererDefThrows()
	{
		var tileset = RegisterTileset("missing-renderer", 64, 64);
		tileset.TileData["0"] = new TileData { Renderer = "missing" };

		var map = new Map(1, 1);
		map.Tilesets.Add(new TileSetData { TileSet = "missing-renderer", FirstGid = 1 });
		map.Tiles[0, 0] = 1;

		Assert.Throws<InvalidOperationException>(() => GameMap.Build(map));
	}

	[Fact]
	public void EmptyTileHasNoTileset()
	{
		RegisterTileset("empty", 16, 16);

		var map = new Map(1, 1);
		map.Tilesets.Add(new TileSetData { TileSet = "empty", FirstGid = 1 });

		var gameMap = GameMap.Build(map);
		var tile = gameMap.GetTile(0, 0);

		Assert.True(tile.IsEmpty);
		Assert.False(tile.HasTileset);
		Assert.Null(tile.Tileset);
		Assert.Equal(-1, tile.LocalId);
	}

	[Fact]
	public void TilesetStartingAtGidZeroIsRejected()
	{
		RegisterTileset("zero-gid", 16, 16);

		var map = new Map(1, 1);
		map.Tilesets.Add(new TileSetData { TileSet = "zero-gid", FirstGid = Map.EmptyGid });

		var exception = Assert.Throws<InvalidOperationException>(() => GameMap.Build(map));
		Assert.Contains("reserved for empty tiles", exception.Message);
	}

	[Fact]
	public void MissingTilesetDefinitionThrows()
	{
		var map = new Map(1, 1);
		map.Tilesets.Add(new TileSetData { TileSet = "missing", FirstGid = 1 });

		Assert.Throws<InvalidOperationException>(() => GameMap.Build(map));
	}

	[Fact]
	public void NonEmptyTileWithoutTilesetThrows()
	{
		var map = new Map(1, 1) { Tiles = { [0, 0] = 1 } };
		Assert.Throws<InvalidOperationException>(() => GameMap.Build(map));
	}

	[Fact]
	public void TilesetSourceTileSizeDoesNotChangeMapGrid()
	{
		RegisterTileset("small", 16, 16);
		var large = RegisterTileset("large", 64, 64, 32, 32);

		var map = new Map(2, 2);
		map.Tilesets.Add(new TileSetData { TileSet = "small", FirstGid = 1 });
		map.Tilesets.Add(new TileSetData { TileSet = "large", FirstGid = 2 });
		map.Tiles[0, 0] = 2;

		var gameMap = GameMap.Build(map);

		Assert.Equal(32, GameMap.TileSize);
		Assert.Equal(new RectangleF(0f, 0f, 64f, 64f), gameMap.Bounds);

		var tile = gameMap.GetTile(0, 0);
		Assert.Same(large, tile.Tileset?.Definition);
		Assert.Equal(0, tile.LocalId);
		Assert.Equal(new RectangleF(0f, 0f, 0.5f, 0.5f), tile.Uv);
	}

	[Fact]
	public void RandomTileVariantsVaryByPositionAndKeepTileData()
	{
		var tileset = RegisterTileset("random-variants", 128, 64);
		tileset.TileData["0"] = new TileData
		{
			Random = [0, 1, 2, 3],
		};

		var map = new Map(8, 8);
		map.Tilesets.Add(new TileSetData { TileSet = "random-variants", FirstGid = 1 });

		for (var y = 0; y < map.Height; y++)
		for (var x = 0; x < map.Width; x++)
			map.Tiles[x, y] = 1;

		var gameMap = GameMap.Build(map);
		var localIds = new HashSet<int>();

		for (var y = 0; y < gameMap.Height; y++)
		for (var x = 0; x < gameMap.Width; x++)
		{
			var tile = gameMap.GetTile(x, y);
			localIds.Add(tile.LocalId);

			Assert.InRange(tile.LocalId, 0, 3);
			Assert.Null(tile.Renderer);
			Assert.Equal("ground", tile.Terrain?.Id);
		}

		Assert.True(localIds.Count > 1);
	}

	private static TilesetDef RegisterTileset(string id, int width, int height, int tileWidth = 16, int tileHeight = 16)
	{
		var definition = new TilesetDef(id)
		{
			SourceMod = TestMods.Base,
			Width = width,
			Height = height,
			TileWidth = tileWidth,
			TileHeight = tileHeight,
		};
		ContentStore.Add(definition);
		return definition;
	}

	private static void RegisterTerrain(string id, PassageMask passage, int moveCost = 1)
	{
		if (ContentStore.Get<TerrainDef>(id) is { } existing)
		{
			existing.Passage = passage;
			existing.MoveCost = moveCost;
			return;
		}

		var definition = new TerrainDef(id)
		{
			SourceMod = TestMods.Base,
			Passage = passage,
			MoveCost = moveCost,
		};
		ContentStore.Add(definition);
	}
}
