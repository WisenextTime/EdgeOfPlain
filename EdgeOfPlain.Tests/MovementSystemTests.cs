using System;
using System.Linq;
using System.Numerics;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Systems;
using EdgeOfPlain.Core.Game.Waypoint;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class MovementSystemTests : PathSystemTestBase
{
	[Fact]
	public void UnitFollowsWaypointsToGoal()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = FastUnit("u");
		var start = new Vector2(16f, 16f);
		var goal = new Vector2(7 * 32f + 16f, 7 * 32f + 16f);

		game.Commands.Enqueue(new SpawnEntityCommand(def, new TransformComponent { Position = start }, 1));
		game.Step();

		var entity = SingleEntity(game);
		var handle = new EntityHandle(entity);

		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(goal)));
		game.Step();

		Assert.True(game.Pathfinding.HasPath(handle));

		StepUntilArrived(game, handle);

		Assert.False(game.Pathfinding.HasPath(handle));
		Assert.True(Vector2.Distance(entity.Get<TransformComponent>().Position, goal) < 32f);
	}

	[Fact]
	public void UnitAvoidsOtherUnitsOnThePath()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = FastUnit("u");
		game.Commands.Enqueue(new SpawnEntityCommand(def, new TransformComponent { Position = new Vector2(16f, 16f) }, 1));
		game.Commands.Enqueue(new SpawnEntityCommand(
			def,
			new TransformComponent { Position = new Vector2(4 * 32f + 16f, 4 * 32f + 16f) },
			1));
		game.Step();

		var a = UnitsNear(game, new Vector2(16f, 16f)).Single();
		var b = UnitsNear(game, new Vector2(4 * 32f + 16f, 4 * 32f + 16f)).Single();

		var goal = new Vector2(7 * 32f + 16f, 7 * 32f + 16f);
		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(new EntityHandle(a)),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(goal)));
		game.Step();

		var handle = new EntityHandle(a);
		var minDistance = float.MaxValue;

		for (var i = 0; i < 300 && game.Pathfinding.HasPath(handle); i++)
		{
			game.Step(0.1f);
			minDistance = Math.Min(
				minDistance,
				Vector2.Distance(
					a.Get<TransformComponent>().Position,
					b.Get<TransformComponent>().Position));
		}

		Assert.False(game.Pathfinding.HasPath(handle));
		Assert.True(minDistance > 8f);
	}

	[Fact]
	public void AlignBeforeTurnRotatesInPlaceFirst()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = FastUnit("u");
		var start = new Vector2(16f, 16f);

		game.Commands.Enqueue(new SpawnEntityCommand(
			def,
			new TransformComponent { Position = start, Rotation = MathF.PI },
			1));
		game.Step();

		var entity = SingleEntity(game);
		var handle = new EntityHandle(entity);

		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(new Vector2(7 * 32f + 16f, 7 * 32f + 16f))));
		game.Step();

		game.Step(0.1f);

		var transform = entity.Get<TransformComponent>();
		Assert.Equal(start, transform.Position);
		Assert.NotEqual(MathF.PI, transform.Rotation);
	}

	[Fact]
	public void ContinuousSteeringMovesWhileTurning()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = new UnitDef("au")
		{
			MaxHealth = 10,
			MoveType = PassageMask.Air,
			AlignBeforeTurn = false,
			MaxVelocity = 60f,
			Acceleration = 100f,
			RotationalVelocity = 10f,
		};
		var start = new Vector2(16f, 16f);

		game.Commands.Enqueue(new SpawnEntityCommand(
			def,
			new TransformComponent { Position = start, Rotation = MathF.PI / 2 },
			1));
		game.Step();

		var entity = SingleEntity(game);
		var handle = new EntityHandle(entity);

		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(new Vector2(7 * 32f + 16f, 7 * 32f + 16f))));
		game.Step();

		game.Step(0.1f);

		var transform = entity.Get<TransformComponent>();
		Assert.NotEqual(start, transform.Position);
	}
}
