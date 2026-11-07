using System;
using System.Collections.Generic;
using EdgeOfPlain.Core.Definitions;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Game.PathFinding;

public sealed class OccupancyGrid
{
	private readonly Dictionary<long, List<EntityHandle>> _buildingsByTile = [];
	private readonly Dictionary<EntityHandle, List<long>> _tilesByBuilding = [];

	public int Version { get; private set; }
	internal IReadOnlyDictionary<EntityHandle, List<long>> Buildings => _tilesByBuilding;

	internal void Restore(IReadOnlyDictionary<EntityHandle, long[]> buildings, int version)
	{
		_buildingsByTile.Clear();
		_tilesByBuilding.Clear();
		foreach (var (building, tiles) in buildings)
		{
			_tilesByBuilding.Add(building, [.. tiles]);
			foreach (var tile in tiles)
			{
				if (!_buildingsByTile.TryGetValue(tile, out var occupants))
					_buildingsByTile.Add(tile, occupants = []);
				if (!occupants.Contains(building)) occupants.Add(building);
			}
		}
		Version = version;
	}

	public bool IsOccupied(int tileX, int tileY) => _buildingsByTile.ContainsKey(Key(tileX, tileY));

	public void Add(EntityHandle building, Footprint footprint, int tileX, int tileY, GameMap map)
	{
		var tiles = _tilesByBuilding.TryGetValue(building, out var existing) ? existing : [];
		var changed = false;

		for (var y = tileY - footprint.Up; y <= tileY + footprint.Down; y++)
		{
			if (y < 0 || y >= map.Height) continue;

			for (var x = tileX - footprint.Left; x <= tileX + footprint.Right; x++)
			{
				if (x < 0 || x >= map.Width) continue;

				changed |= AddTile(building, Key(x, y), tiles);
			}
		}

		if (!changed) return;

		_tilesByBuilding[building] = tiles;
		Version++;
	}

	public void Add(EntityHandle building, Footprint footprint, Vector2d position, int radius, GameMap map)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(radius);
		var tile = map.WorldToTile(position);
		Add(building, footprint, tile.X, tile.Y, map);
		if (radius == 0) return;
		var reach = ((long)radius + GameMap.TileSize - 1) / GameMap.TileSize + 1;
		var minX = (int)Math.Max(0, tile.X - reach);
		var minY = (int)Math.Max(0, tile.Y - reach);
		var maxX = (int)Math.Min(map.Width - 1L, tile.X + reach);
		var maxY = (int)Math.Min(map.Height - 1L, tile.Y + reach);
		var tiles = _tilesByBuilding.TryGetValue(building, out var existing) ? existing : [];
		var changed = false;
		for (var y = minY; y <= maxY; y++)
		for (var x = minX; x <= maxX; x++)
		{
			var dx = FixedMath.Max(Fixed64.Zero, FixedMath.Max((Fixed64)x * GameMap.TileSize - position.X,
				position.X - (Fixed64)(x + 1) * GameMap.TileSize)) / (Fixed64)radius;
			var dy = FixedMath.Max(Fixed64.Zero, FixedMath.Max((Fixed64)y * GameMap.TileSize - position.Y,
				position.Y - (Fixed64)(y + 1) * GameMap.TileSize)) / (Fixed64)radius;
			if (dx >= Fixed64.One || dy >= Fixed64.One || dx * dx + dy * dy >= Fixed64.One) continue;
			changed |= AddTile(building, Key(x, y), tiles);
		}
		if (!changed) return;
		_tilesByBuilding[building] = tiles;
		Version++;
	}

	private bool AddTile(EntityHandle building, long tile, List<long> tiles)
	{
		if (!_buildingsByTile.TryGetValue(tile, out var buildings))
			_buildingsByTile.Add(tile, buildings = []);
		if (buildings.Contains(building)) return false;
		buildings.Add(building);
		tiles.Add(tile);
		return true;
	}

	public void Remove(EntityHandle building)
	{
		if (!_tilesByBuilding.Remove(building, out var tiles)) return;

		foreach (var key in tiles)
		{
			var buildings = _buildingsByTile[key];
			buildings.Remove(building);

			if (buildings.Count == 0) _buildingsByTile.Remove(key);
		}

		Version++;
	}

	private static long Key(int x, int y) => (uint)x | (long)y << 32;
}
