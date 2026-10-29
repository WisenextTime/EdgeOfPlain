using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Waypoint;

namespace EdgeOfPlain.Core.Content.Systems.PathFinding;

public sealed class PathfindingSystem : ICommandApplier<WaypointCommand>
{
	public void Apply(World world, in WaypointCommand command)
	{
		var pathfinding = WorldPathfinding.TryGet(world);
		if (pathfinding is null) return;

		if (command.Target is not GroundTarget ground) return;

		var goal = pathfinding.Map.WorldToTile(ground.Position);

		foreach (var handle in command.Units.Entities)
		{
			var entity = handle.Value;
			if (!entity.IsAlive()) continue;

			var move = entity.Get<MoveTypeComponent>();
			var goalPoint = (goal.X, goal.Y);
			var goalTile = pathfinding.ClampGoal(goalPoint, move.Type);
			var packed = FlowField.Pack(goalTile.X, goalTile.Y);
			var field = pathfinding.Fields.GetOrCreate(packed, move.Type);

			var tile = pathfinding.Map.WorldToTile(entity.Get<TransformComponent>().Position);
			var waypoints = BuildPath(field, pathfinding.Map, tile.X, tile.Y);

			pathfinding.SetPath(handle, waypoints);
		}
	}

	private static List<Vector2> BuildPath(FlowField field, GameMap map, int startX, int startY)
	{
		var waypoints = new List<Vector2>();
		var (x, y) = (startX, startY);
		var (goalX, goalY) = FlowField.Unpack(field.GoalTile);

		while (!(x == goalX && y == goalY))
		{
			var next = field.Next(x, y);
			if (next is null) break;

			(x, y) = next.Value;
			waypoints.Add(map.TileCenter(x, y));
		}

		return Compress(waypoints);
	}

	private static List<Vector2> Compress(List<Vector2> waypoints)
	{
		var result = new List<Vector2>(waypoints.Count);

		foreach (var waypoint in waypoints)
		{
			if (result.Count < 2)
			{
				result.Add(waypoint);
				continue;
			}

			var previous = result[^2];
			var current = result[^1];
			var directionA = current - previous;
			var directionB = waypoint - current;

			if (Vector2.Dot(directionA, directionB) > 0.999f * directionA.Length() * directionB.Length())
				result.RemoveAt(result.Count - 1);

			result.Add(waypoint);
		}

		return result;
	}
}
