using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Systems;
using EdgeOfPlain.Core.Game.Waypoint;
using FixedMathSharp;
using FixedMathSharp.Geometry;
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
		var start = V(16f, 16f);
		var goal = V(7 * 32f + 16f, 7 * 32f + 16f);

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
		Assert.True(entity.Get<TransformComponent>().Position.Distance(goal) < (Fixed64)32);
	}

	[Fact]
	public void UnitAvoidsOtherUnitsOnThePath()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = FastUnit("u");
		game.Commands.Enqueue(new SpawnEntityCommand(def, new TransformComponent { Position = V(16f, 16f) }, 1));
		game.Commands.Enqueue(new SpawnEntityCommand(
			def,
			new TransformComponent { Position = V(4 * 32f + 16f, 4 * 32f + 16f) },
			1));
		game.Step();

		var a = UnitsNear(game, V(16f, 16f)).Single();
		var b = UnitsNear(game, V(4 * 32f + 16f, 4 * 32f + 16f)).Single();

		var goal = V(7 * 32f + 16f, 7 * 32f + 16f);
		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(new EntityHandle(a)),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(goal)));
		game.Step();

		var handle = new EntityHandle(a);
		var minDistance = Fixed64.MaxValue;

		for (var i = 0; i < 300 && game.Pathfinding.HasPath(handle); i++)
		{
			game.Step((Fixed64)0.1);
			minDistance = FixedMath.Min(
				minDistance,
				a.Get<TransformComponent>().Position.Distance(b.Get<TransformComponent>().Position));
		}

		Assert.False(game.Pathfinding.HasPath(handle));
		Assert.True(minDistance > (Fixed64)8);
	}

	[Fact]
	public void AlignBeforeTurnRotatesInPlaceFirst()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = new UnitDef("aligner")
		{
			MaxHealth = 10,
			MaxVelocity = 60f,
			Acceleration = 100f,
			RotationalVelocity = 10f,
			AlignBeforeTurn = true,
		};
		var start = V(16f, 16f);

		game.Commands.Enqueue(new SpawnEntityCommand(
			def,
			new TransformComponent { Position = start, Rotation = (Fixed64)MathF.PI },
			1));
		game.Step();

		var entity = SingleEntity(game);
		var handle = new EntityHandle(entity);

		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(V(7 * 32f + 16f, 7 * 32f + 16f))));
		game.Step();

		game.Step((Fixed64)0.1);

		var transform = entity.Get<TransformComponent>();
		Assert.Equal(start, transform.Position);
		Assert.NotEqual((Fixed64)MathF.PI, transform.Rotation);
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
		var start = V(16f, 16f);

		game.Commands.Enqueue(new SpawnEntityCommand(
			def,
			new TransformComponent { Position = start, Rotation = (Fixed64)(MathF.PI / 2) },
			1));
		game.Step();

		var entity = SingleEntity(game);
		var handle = new EntityHandle(entity);

		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(V(7 * 32f + 16f, 7 * 32f + 16f))));
		game.Step();

		game.Step((Fixed64)0.1);

		var transform = entity.Get<TransformComponent>();
		Assert.NotEqual(start, transform.Position);
	}

	[Fact]
	public void TurnRateSurvivesInPlaceAlignment()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = new UnitDef("turner")
		{
			MaxHealth = 10,
			MaxVelocity = 60f,
			Acceleration = 100f,
			RotationalVelocity = 10f,
			AlignBeforeTurn = true,
		};
		game.Commands.Enqueue(new SpawnEntityCommand(
			def,
			new TransformComponent { Position = V(16f, 16f), Rotation = (Fixed64)MathF.PI },
			1));
		game.Step();

		var entity = SingleEntity(game);
		var handle = new EntityHandle(entity);

		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(V(7 * 32f + 16f, 7 * 32f + 16f))));
		game.Step();

		for (var i = 0; i < 4; i++) game.Step((Fixed64)0.1);

		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(V(16f, 7 * 32f + 16f))));
		game.Step();
		game.Step((Fixed64)0.1);

		var rotation = entity.Get<TransformComponent>().Rotation;
		Assert.True(rotation > (Fixed64)0.5, $"Rotation {rotation} should keep full turn rate after alignment.");
	}

	[Fact]
	public void UnitArrivesWithVelocityZeroed()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = FastUnit("u");
		game.Commands.Enqueue(new SpawnEntityCommand(def, new TransformComponent { Position = V(16f, 16f) }, 1));
		game.Step();

		var entity = SingleEntity(game);
		var handle = new EntityHandle(entity);

		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(V(7 * 32f + 16f, 7 * 32f + 16f))));
		game.Step();

		StepUntilArrived(game, handle);

		Assert.False(game.Pathfinding.HasPath(handle));
		Assert.Equal(Fixed64.Zero, entity.Get<VelocityComponent>().Velocity);
	}

	[Fact]
	public void DenseQueueDoesNotCircleForever()
	{
		var map = PathTestMaps.Build(16, 16, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = FastUnit("u");
		var goal = V(15 * 32f + 16f, 15 * 32f + 16f);

		const int cols = 6;

		for (var row = 0; row < cols; row++)
		for (var col = 0; col < cols; col++)
			game.Commands.Enqueue(new SpawnEntityCommand(
				def,
				new TransformComponent { Position = V(col * 32f + 16f, row * 32f + 16f) },
				1));

		game.Step();

		var description = new QueryDescription().WithAll<TransformComponent>();
		var handles = new List<EntityHandle>();
		game.World.Query(description, (Entity entity, ref TransformComponent _) => handles.Add(new EntityHandle(entity)));

		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handles.ToArray()),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(goal)));
		game.Step();

		for (var i = 0; i < 900 && handles.Any(handle => game.Pathfinding.HasPath(handle)); i++)
			game.Step((Fixed64)0.1);

		Assert.All(handles, handle => Assert.False(game.Pathfinding.HasPath(handle)));
	}

	[Fact]
	public void ComputePathRecomputesAfterOccupancyChange()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		RegisterSystems(game);

		var def = FastUnit("u");
		game.Commands.Enqueue(new SpawnEntityCommand(def, new TransformComponent { Position = V(16f, 16f) }, 1));
		game.Step();

		var entity = SingleEntity(game);
		var handle = new EntityHandle(entity);

		game.Commands.Enqueue(new WaypointCommand(
			new UnitGroup(handle),
			WaypointType.Move,
			WaypointAddType.Replace,
			new GroundTarget(V(7 * 32f + 16f, 7 * 32f + 16f))));
		game.Step();

		Assert.True(game.Pathfinding.HasPath(handle));

		var blocked = V(3 * 32f + 16f, 3 * 32f + 16f);
		game.Commands.Enqueue(new SpawnEntityCommand(
			new BuildingDef("tower") { MaxHealth = 100, Collision = new Footprint(0, 0, 0, 0) },
			new TransformComponent { Position = blocked },
			1));
		game.Step();

		Pathfinding.ComputePath(game.World, handle);

		var path = game.Pathfinding.Paths[handle];
		Assert.NotEmpty(path.Waypoints);
		Assert.DoesNotContain(blocked, path.Waypoints);
	}
}
