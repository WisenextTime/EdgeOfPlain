using System;
using System.Collections.Generic;
using Arch.Core;
using EdgeOfPlain.Core.Definitions.Worlds;
using FixedMathSharp;

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

	public void SetPath(EntityHandle entity, List<Vector2d> waypoints, long targetTile)
		=> _paths[entity] = new EntityPath(waypoints, targetTile);

	public void Repath(EntityHandle entity, List<Vector2d> waypoints)
	{
		if (!_paths.TryGetValue(entity, out var path)) return;

		path.Waypoints.Clear();
		path.Waypoints.AddRange(waypoints);
		path.Index = 0;
	}

	public void ClearPath(EntityHandle entity) => _paths.Remove(entity);

	public int Count => _paths.Count;

	public (int X, int Y) ClampGoal((int X, int Y) goal, PassageMask mask, int collisionRadius = 0, bool enableCollision = true)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(collisionRadius);
		if (Cost(goal.X, goal.Y, mask, collisionRadius, enableCollision) > 0) return goal;

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

					if (Cost(x, y, mask, collisionRadius, enableCollision) > 0) return (x, y);
				}
			}
		}

		return goal;
	}

	public int Cost(int tileX, int tileY, PassageMask mask) => Cost(tileX, tileY, mask, 0, true);

	public int Cost(int tileX, int tileY, PassageMask mask, int radius, bool enableCollision)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(radius);
		var centerCost = PointCost(tileX, tileY, mask, enableCollision);
		if (centerCost == 0 || !enableCollision || radius == 0) return centerCost;
		var centerX = (long)tileX * GameMap.TileSize + GameMap.TileSize / 2;
		var centerY = (long)tileY * GameMap.TileSize + GameMap.TileSize / 2;
		if (centerX < radius || centerY < radius || centerX + radius > (long)Map.Width * GameMap.TileSize ||
		    centerY + radius > (long)Map.Height * GameMap.TileSize) return 0;

		var minX = (int)((centerX - radius) / GameMap.TileSize);
		var minY = (int)((centerY - radius) / GameMap.TileSize);
		var maxX = (int)Math.Min(Map.Width - 1L, (centerX + radius) / GameMap.TileSize);
		var maxY = (int)Math.Min(Map.Height - 1L, (centerY + radius) / GameMap.TileSize);
		var squaredRadius = (long)radius * radius;
		for (var y = minY; y <= maxY; y++)
		for (var x = minX; x <= maxX; x++)
		{
			var dx = Math.Max(0, Math.Max((long)x * GameMap.TileSize - centerX, centerX - ((long)x + 1) * GameMap.TileSize));
			var dy = Math.Max(0, Math.Max((long)y * GameMap.TileSize - centerY, centerY - ((long)y + 1) * GameMap.TileSize));
			if (dx * dx + dy * dy >= squaredRadius) continue;
			if (PointCost(x, y, mask, true) == 0) return 0;
		}
		return centerCost;
	}

	private int PointCost(int tileX, int tileY, PassageMask mask, bool enableCollision)
	{
		var best = 0;

		for (var bit = 1; bit <= 8; bit <<= 1)
		{
			if ((mask & (PassageMask)bit) == 0) continue;

			var cost = Map.GetMoveCost(tileX, tileY, (PassageMask)bit);

			if (enableCollision && Occupancy.IsOccupied(tileX, tileY) && (PassageMask)bit != PassageMask.Air)
				cost = 0;

			if (cost > 0)
				best = best == 0 ? cost : Math.Min(best, cost);
		}

		return best;
	}

	public Vector2d TileCenter(int tileX, int tileY) => Map.TileCenter(tileX, tileY);
}

public sealed class EntityPath(List<Vector2d> waypoints, long targetTile)
{

	public List<Vector2d> Waypoints { get; } = waypoints;
	public long TargetTile { get; } = targetTile;
	public int Index { get; set; }
	public int RepathAttempts { get; set; }
}
