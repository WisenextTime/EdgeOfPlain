using System;
using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Game.PathFinding;

public sealed class WorldPathfinding
{
	private static readonly Dictionary<World, WorldPathfinding> _byWorld = [];

	private readonly Dictionary<EntityHandle, EntityPath> _paths = [];

	private WorldPathfinding(GameMap map)
	{
		Map = map;
		Occupancy = new OccupancyGrid();
		Fields = new FlowFieldCache(map, Occupancy, Cost);
	}

	public GameMap Map { get; }
	public OccupancyGrid Occupancy { get; }
	public FlowFieldCache Fields { get; }

	public IReadOnlyDictionary<EntityHandle, EntityPath> Paths => _paths;

	public static WorldPathfinding Create(World world, GameMap map)
	{
		var pathfinding = new WorldPathfinding(map);
		_byWorld[world] = pathfinding;
		return pathfinding;
	}

	public static WorldPathfinding? TryGet(World world)
		=> _byWorld.GetValueOrDefault(world);

	public static void Dispose(World world) => _byWorld.Remove(world);

	public bool HasPath(EntityHandle entity) => _paths.ContainsKey(entity);

	public void SetPath(EntityHandle entity, List<Vector2> waypoints, long targetTile)
		=> _paths[entity] = new EntityPath(waypoints, targetTile);

	public void Repath(EntityHandle entity, List<Vector2> waypoints)
	{
		if (!_paths.TryGetValue(entity, out var path)) return;

		path.Waypoints.Clear();
		path.Waypoints.AddRange(waypoints);
		path.Index = 0;
	}

	public void ClearPath(EntityHandle entity) => _paths.Remove(entity);

	public int Count => _paths.Count;

	public (int X, int Y) ClampGoal((int X, int Y) goal, PassageMask mask)
	{
		if (Cost(goal.X, goal.Y, mask) > 0) return goal;

		for (var radius = 1; radius <= Map.Width + Map.Height; radius++)
		{
			for (var dx = -radius; dx <= radius; dx++)
			{
				var x = goal.X + dx;
				if ((uint)x >= (uint)Map.Width) continue;

				var dy = radius - Math.Abs(dx);

				foreach (var y in new[] { goal.Y + dy, goal.Y - dy })
				{
					if ((uint)y >= (uint)Map.Height) continue;

					if (Cost(x, y, mask) > 0) return (x, y);
				}
			}
		}

		return goal;
	}

	public int Cost(int tileX, int tileY, PassageMask mask)
	{
		var best = 0;

		for (var bit = 1; bit <= 8; bit <<= 1)
		{
			if ((mask & (PassageMask)bit) == 0) continue;

			var cost = Map.GetMoveCost(tileX, tileY, (PassageMask)bit);

			if (Occupancy.IsOccupied(tileX, tileY) && (PassageMask)bit != PassageMask.Air)
				cost = 0;

			if (cost > 0)
				best = best == 0 ? cost : Math.Min(best, cost);
		}

		return best;
	}

	public Vector2 TileCenter(int tileX, int tileY) => Map.TileCenter(tileX, tileY);
}

public sealed class EntityPath(List<Vector2> waypoints, long targetTile)
{

	public List<Vector2> Waypoints { get; } = waypoints;
	public long TargetTile { get; } = targetTile;
	public int Index { get; set; }
	public int RepathAttempts { get; set; }
}