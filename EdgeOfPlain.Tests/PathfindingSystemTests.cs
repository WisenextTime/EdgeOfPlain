using System.Numerics;
using Arch.Core;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Systems;
using EdgeOfPlain.Core.Game.Waypoint;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class PathfindingSystemTests : PathSystemTestBase
{
	[Fact]
	public void MoveCommandProducesPathToGoal()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = FastUnit("u");
		game.Commands.Enqueue(new SpawnEntityCommand(def, new TransformComponent { Position = new Vector2(16f, 16f) }, 1));
		game.Step();

		var entity = SingleEntity(game);
		var handle = new EntityHandle(entity);

		var goal = new Vector2(7 * 32f + 16f, 7 * 32f + 16f);
		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(goal)));
		game.Step();

		var path = game.Pathfinding.Paths[handle];
		Assert.NotEmpty(path.Waypoints);
		Assert.Equal(goal, path.Waypoints[^1]);
	}

	[Fact]
	public void PathAvoidsWalls()
	{
		var map = PathTestMaps.Build(
			5, 5,
			(x, y) => (x, y) == (2, 2) ? PathTestMaps.CliffKind : PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = FastUnit("u");
		game.Commands.Enqueue(new SpawnEntityCommand(def, new TransformComponent { Position = new Vector2(16f, 16f) }, 1));
		game.Step();

		var entity = SingleEntity(game);
		var handle = new EntityHandle(entity);

		var goal = new Vector2(4 * 32f + 16f, 2 * 32f + 16f);
		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(goal)));
		game.Step();

		var path = game.Pathfinding.Paths[handle];
		Assert.True(path.Waypoints.Count > 1);
		Assert.Equal(goal, path.Waypoints[^1]);
	}

	[Fact]
	public void ReplaceProducesNewPath()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = FastUnit("u");
		game.Commands.Enqueue(new SpawnEntityCommand(def, new TransformComponent { Position = new Vector2(16f, 16f) }, 1));
		game.Step();

		var entity = SingleEntity(game);
		var handle = new EntityHandle(entity);

		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(new Vector2(7 * 32f + 16f, 7 * 32f + 16f))));
		game.Step();

		var first = game.Pathfinding.Paths[handle];

		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(new Vector2(16f, 7 * 32f + 16f))));
		game.Step();

		var second = game.Pathfinding.Paths[handle];
		Assert.NotSame(first, second);
		Assert.NotEqual(first.Waypoints[^1], second.Waypoints[^1]);
	}

	[Fact]
	public void FlowFieldCacheSharesFieldsForSameGoalAndMask()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		using var world = World.Create();
		var pathfinding = WorldPathfinding.Create(world, map);

		var goal = FlowField.Pack(7, 7);

		Assert.Same(
			pathfinding.Fields.GetOrCreate(goal, PassageMask.Ground),
			pathfinding.Fields.GetOrCreate(goal, PassageMask.Ground));
	}
}
