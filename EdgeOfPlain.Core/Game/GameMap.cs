using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Exceptions;
using EdgeOfPlain.Core.Framework.Mods;
using FixedMathSharp;
using FixedMathSharp.Geometry;

namespace EdgeOfPlain.Core.Game;

public sealed class GameMap
{
	public const int DefaultChunkSize = 32;
	public const int TileSize = 32;

	private readonly ushort[,] _tiles;
	private readonly MapTileset[] _tilesets;
	private readonly TileRendererDef[] _renderers;

	private GameMap(
		ushort[,] tiles,
		MapTileset[] tilesets,
		TileRendererDef[] renderers,
		MapChunk[] chunks,
		int chunkSize)
	{
		_tiles = tiles;
		_tilesets = tilesets;
		_renderers = renderers;
		Chunks = chunks;
		ChunkSize = chunkSize;
		Bounds = new RectangleF(0f, 0f, Width * (float)TileSize, Height * (float)TileSize);
	}

	public int Width => _tiles.GetLength(0);
	public int Height => _tiles.GetLength(1);
	public int ChunkSize { get; }
	public RectangleF Bounds { get; }
	public IReadOnlyList<MapTileset> Tilesets => _tilesets;
	public IReadOnlyList<TileRendererDef> Renderers => _renderers;
	public IReadOnlyList<MapChunk> Chunks { get; }

	public static GameMap Build(Map map, int chunkSize = DefaultChunkSize)
	{
		if (chunkSize <= 0)
			throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Chunk size must be positive.");

		var sourceTiles = map.Tiles ?? throw new ArgumentException("Map has no tile grid.", nameof(map));
		var tiles = (ushort[,])sourceTiles.Clone();
		var tilesets = BuildTilesets(map.Tilesets);
		var renderers = CollectRenderers(tilesets);

		ValidateTiles(tiles, tilesets);

		var chunks = BuildChunks(tiles.GetLength(0), tiles.GetLength(1), chunkSize);
		return new GameMap(tiles, tilesets, renderers, chunks, chunkSize);
	}

	public bool IsInside(int tileX, int tileY)
		=> (uint)tileX < (uint)Width && (uint)tileY < (uint)Height;

	public ushort GetTileId(int tileX, int tileY)
	{
		if (!IsInside(tileX, tileY))
			throw new ArgumentOutOfRangeException(nameof(tileX), $"Tile ({tileX},{tileY}) is outside the map.");

		return _tiles[tileX, tileY];
	}

	public MapTile GetTile(int tileX, int tileY)
	{
		if (!IsInside(tileX, tileY))
			throw new ArgumentOutOfRangeException(nameof(tileX), $"Tile ({tileX},{tileY}) is outside the map.");

		return ResolveTile(tileX, tileY, _tiles[tileX, tileY]);
	}

	public bool TryGetTile(int tileX, int tileY, out MapTile tile)
	{
		if (!IsInside(tileX, tileY))
		{
			tile = default;
			return false;
		}

		tile = ResolveTile(tileX, tileY, _tiles[tileX, tileY]);
		return true;
	}

	public bool IsPassable(int tileX, int tileY, PassageMask passage)
		=> GetMoveCost(tileX, tileY, passage) > 0;

	public int GetMoveCost(int tileX, int tileY, PassageMask passage)
		=> TryGetTile(tileX, tileY, out var tile) && tile.Terrain is { } terrain ? terrain.GetMoveCost(passage) : 0;

	public Vector2d TileToWorld(int tileX, int tileY)
		=> new((Fixed64)(tileX * TileSize), (Fixed64)(tileY * TileSize));

	public Vector2d TileCenter(int tileX, int tileY)
		=> new(((Fixed64)tileX + Fixed64.Half) * (Fixed64)TileSize, ((Fixed64)tileY + Fixed64.Half) * (Fixed64)TileSize);

	public Point WorldToTile(Vector2d world)
		=> new((int)FixedMath.Floor(world.X / (Fixed64)TileSize), (int)FixedMath.Floor(world.Y / (Fixed64)TileSize));

	public RectangleF GetTileBounds(int tileX, int tileY)
		=> new(
			tileX * (float)TileSize,
			tileY * (float)TileSize,
			TileSize,
			TileSize);

	private MapTile ResolveTile(int tileX, int tileY, ushort id)
	{
		if (id == Map.EmptyGid)
			return new MapTile(0, null, -1, -1, -1, RectangleF.Empty, null, null);

		var index = FindTilesetIndex(_tilesets, id);
		if (index < 0)
			throw new InvalidOperationException($"Tile id {id} has no matching tileset.");

		var tileset = _tilesets[index];
		var baseLocalId = tileset.LocalId(id);
		var tileData = tileset.GetTileData(baseLocalId);
		var localId = SelectRandomVariant(tileX, tileY, baseLocalId, tileData.Random, tileset.TileCount);
		var column = localId % tileset.Columns;
		var row = localId / tileset.Columns;

		var terrain = ContentStore.Get<TerrainDef>(tileData.Terrain, tileset.Definition) ??
		              throw new MapException($"Unknow terrain {tileData.Terrain}");

		TileRendererDef? renderer = null;

		if (!string.IsNullOrWhiteSpace(tileData.Renderer))
		{
			renderer = ContentStore.Get<TileRendererDef>(tileData.Renderer, tileset.Definition) ??
			           throw new MapException($"Unknow renderer {tileData.Renderer}");
		}

		return new MapTile(
			id,
			tileset,
			localId,
			column,
			row,
			tileset.GetUv(localId),
			terrain,
			renderer);
	}

	private static int SelectRandomVariant(
		int tileX,
		int tileY,
		int fallbackLocalId,
		int[]? variants,
		int tileCount)
	{
		if (variants is not { Length: > 0 })
			return fallbackLocalId;

		var hash = unchecked((uint)(tileX * 73856093) ^ (uint)(tileY * 19349663));
		hash ^= hash >> 13;
		hash *= 1274126177;
		hash ^= hash >> 16;

		var variant = variants[hash % (uint)variants.Length];

		if ((uint)variant >= (uint)tileCount)
			throw new InvalidOperationException(
				$"Random tile variant {variant} is outside the tileset (tile count {tileCount}).");

		return variant;
	}

	private static MapTileset[] BuildTilesets(List<TileSetData> sources)
	{
		var result = new List<MapTileset>(sources.Count);

		foreach (var source in sources)
		{
			if (string.IsNullOrWhiteSpace(source.TileSet))
				throw new InvalidOperationException("Map contains a tileset with an empty id.");

			if (source.FirstGid == Map.EmptyGid)
				throw new InvalidOperationException(
					$"Tileset '{source.TileSet}' starts at gid {Map.EmptyGid}, which is reserved for empty tiles; use 1 or later.");

			var definition = ContentStore.Get<TilesetDef>(source.TileSet)
			                 ?? throw new InvalidOperationException($"Tileset '{source.TileSet}' is not registered.");
			result.Add(new MapTileset(source.FirstGid, definition));
		}

		result.Sort(static (left, right) => left.FirstGid.CompareTo(right.FirstGid));

		for (var index = 1; index < result.Count; index++)
		{
			if (result[index - 1].FirstGid == result[index].FirstGid)
				throw new InvalidOperationException($"Map contains duplicate tileset first gid {result[index].FirstGid}.");
		}

		return [.. result];
	}

	private static TileRendererDef[] CollectRenderers(MapTileset[] tilesets)
	{
		var renderers = new Dictionary<ContentKey, TileRendererDef>();

		foreach (var tileset in tilesets)
		{
			foreach (var tileData in tileset.TileDataValues)
			{
				if (string.IsNullOrWhiteSpace(tileData.Renderer)) continue;

				var renderer = ContentStore.Get<TileRendererDef>(tileData.Renderer, tileset.Definition)
				               ?? throw new InvalidOperationException($"Tile renderer '{tileData.Renderer}' is not registered.");

				renderers.TryAdd(renderer.Key, renderer);
			}
		}

		var keys = new List<ContentKey>(renderers.Keys);
		keys.Sort();

		var result = new List<TileRendererDef>(keys.Count);

		foreach (var key in keys) result.Add(renderers[key]);

		return [.. result];
	}

	private static void ValidateTiles(ushort[,] tiles, MapTileset[] tilesets)
	{
		for (var y = 0; y < tiles.GetLength(1); y++)
		for (var x = 0; x < tiles.GetLength(0); x++)
		{
			var id = tiles[x, y];
			if (id == Map.EmptyGid || FindTilesetIndex(tilesets, id) >= 0) continue;

			throw new InvalidOperationException($"Tile ({x},{y}) has id {id}, but no matching tileset is registered.");
		}
	}

	private static MapChunk[] BuildChunks(int width, int height, int chunkSize)
	{
		if (width <= 0 || height <= 0)
			return [];

		var chunks = new List<MapChunk>(
			((width + chunkSize - 1) / chunkSize) * ((height + chunkSize - 1) / chunkSize));

		for (var tileY = 0; tileY < height; tileY += chunkSize)
		{
			var rows = Math.Min(chunkSize, height - tileY);

			for (var tileX = 0; tileX < width; tileX += chunkSize)
			{
				var columns = Math.Min(chunkSize, width - tileX);

				chunks.Add(new MapChunk(
					tileX,
					tileY,
					columns,
					rows,
					new RectangleF(
						tileX * (float)TileSize,
						tileY * (float)TileSize,
						columns * (float)TileSize,
						rows * (float)TileSize)));
			}
		}

		return [.. chunks];
	}

	private static int FindTilesetIndex(MapTileset[] tilesets, ushort id)
	{
		var low = 0;
		var high = tilesets.Length - 1;

		while (low <= high)
		{
			var middle = low + (high - low) / 2;

			if (tilesets[middle].FirstGid <= id)
				low = middle + 1;
			else
				high = middle - 1;
		}

		return high >= 0 && tilesets[high].Contains(id) ? high : -1;
	}
}

public readonly record struct MapChunk(int TileX, int TileY, int Columns, int Rows, RectangleF Bounds)
{
	public int TileCount => Columns * Rows;
	public Vector2 Position => new Vector2(TileX, TileY);
}

public readonly record struct MapTile(
	ushort Id,
	MapTileset? Tileset,
	int LocalId,
	int Column,
	int Row,
	RectangleF Uv,
	TerrainDef? Terrain,
	TileRendererDef? Renderer)
{
	public bool IsEmpty => Tileset is null;
	public bool HasTileset => Tileset is not null;
}

public sealed class MapTileset
{
	private readonly Dictionary<int, TileData> _tileData;

	internal MapTileset(ushort firstGid, TilesetDef definition)
	{
		FirstGid = firstGid;
		Definition = definition;

		if (definition.Width <= 0 || definition.Height <= 0)
			throw new InvalidOperationException($"Tileset '{definition.Id}' must have a positive image size.");

		if (definition.TileWidth <= 0 || definition.TileHeight <= 0)
			throw new InvalidOperationException($"Tileset '{definition.Id}' must have a positive tile size.");

		if (definition.LeftMargin < 0 || definition.TopMargin < 0 ||
		    definition.RightMargin < 0 || definition.BottomMargin < 0)
			throw new InvalidOperationException($"Tileset '{definition.Id}' has a negative margin.");

		var strideX = definition.TileWidth + definition.HorizontalSpacing;
		var strideY = definition.TileHeight + definition.VerticalSpacing;

		if (strideX <= 0 || strideY <= 0)
			throw new InvalidOperationException($"Tileset '{definition.Id}' has an invalid spacing.");

		Columns = (definition.Width - definition.LeftMargin - definition.RightMargin + definition.HorizontalSpacing) / strideX;
		Rows = (definition.Height - definition.TopMargin - definition.BottomMargin + definition.VerticalSpacing) / strideY;

		if (Columns <= 0 || Rows <= 0)
			throw new InvalidOperationException($"Tileset '{definition.Id}' does not contain a complete tile.");

		_tileData = new Dictionary<int, TileData>(definition.TileData.Count);

		foreach (var pair in definition.TileData)
		{
			if (!int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var localId) ||
			    localId < 0 || localId >= TileCount)
				continue;

			if (pair.Value.Random is { } variants)
				foreach (var variant in variants)
				{
					if ((uint)variant >= (uint)TileCount)
						throw new InvalidOperationException(
							$"Tileset '{definition.Id}' tile {localId} has random variant {variant}, " +
							$"but the tileset only contains {TileCount} tile(s).");
				}

			_tileData[localId] = pair.Value;
		}
	}

	public ushort FirstGid { get; }
	public TilesetDef Definition { get; }
	public int Columns { get; }
	public int Rows { get; }
	public int TileWidth => Definition.TileWidth;
	public int TileHeight => Definition.TileHeight;
	public int TileCount => Columns * Rows;
	public IEnumerable<TileData> TileDataValues => _tileData.Values;

	public string TextureKey => Definition.ResolveResourcePath(Definition.Texture);

	public bool Contains(ushort id)
		=> id != Map.EmptyGid && id >= FirstGid && id - FirstGid < TileCount;

	public int LocalId(ushort id) => id - FirstGid;

	public TileData GetTileData(int localId) => _tileData.GetValueOrDefault(localId);

	public RectangleF GetUv(int localId)
	{
		if ((uint)localId >= (uint)TileCount)
			throw new ArgumentOutOfRangeException(nameof(localId), localId, "Tile local id is outside the tileset.");

		var column = localId % Columns;
		var row = localId / Columns;
		var left = Definition.LeftMargin + column * (TileWidth + Definition.HorizontalSpacing);
		var top = Definition.TopMargin + row * (TileHeight + Definition.VerticalSpacing);

		return new RectangleF(
			left / (float)Definition.Width,
			top / (float)Definition.Height,
			TileWidth / (float)Definition.Width,
			TileHeight / (float)Definition.Height);
	}
}
