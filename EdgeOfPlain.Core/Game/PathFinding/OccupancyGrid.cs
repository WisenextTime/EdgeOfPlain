using System.Collections.Generic;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Game.PathFinding;

public sealed class OccupancyGrid
{
	private readonly Dictionary<long, List<EntityHandle>> _buildingsByTile = [];
	private readonly Dictionary<EntityHandle, List<long>> _tilesByBuilding = [];

	public int Version { get; private set; }

	public bool IsOccupied(int tileX, int tileY) => _buildingsByTile.ContainsKey(Key(tileX, tileY));

	public void Add(EntityHandle building, Footprint footprint, int tileX, int tileY, GameMap map)
	{
		var tiles = new List<long>();

		for (var y = tileY - footprint.Up; y <= tileY + footprint.Down; y++)
		{
			if (y < 0 || y >= map.Height) continue;

			for (var x = tileX - footprint.Left; x <= tileX + footprint.Right; x++)
			{
				if (x < 0 || x >= map.Width) continue;

				var key = Key(x, y);

				if (!_buildingsByTile.TryGetValue(key, out var buildings))
				{
					buildings = [];
					_buildingsByTile.Add(key, buildings);
				}

				if (buildings.Contains(building)) continue;

				buildings.Add(building);
				tiles.Add(key);
			}
		}

		if (tiles.Count == 0) return;

		_tilesByBuilding[building] = tiles;
		Version++;
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
