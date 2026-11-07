using System;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Content.Systems.Applier;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Waypoint;
using FixedMathSharp;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class CollisionTests : PathSystemTestBase
{
	[Fact]
	public void DefinitionsAndNewComponentsHaveCollisionEnabledWithRadius16()
	{
		var unit = new UnitDef("unit");
		var building = new BuildingDef("building");
		Assert.True(unit.EnableCollision);
		Assert.Equal(16, unit.Radius);
		Assert.True(building.EnableCollision);
		Assert.Equal(16, building.Radius);
		var component = new CollisionComponent();
		Assert.True(component.EnableCollision);
		Assert.Equal(16, component.Radius);
	}

	[Fact]
	public void TomlSettingsAndDefaultsReachTheSpawnedComponent()
	{
		RegistryProbes.Ensure(typeof(UnitDef));
		var defaults = Assert.IsType<UnitDef>(ContentSerializer.Deserialize<ContentDef>(
			"type = 'unit'\nid = 'default'\nmaxHealth = 10\ntexture = 'unit'"));
		Assert.True(defaults.EnableCollision);
		Assert.Equal(16, defaults.Radius);
		var definition = Assert.IsType<UnitDef>(ContentSerializer.Deserialize<ContentDef>(
			"type = 'unit'\nid = 'custom'\nmaxHealth = 10\ntexture = 'unit'\nenableCollision = false\nradius = 29"));
		using var game = new GameWorld(PathTestMaps.Build(5, 5, (_, _) => PathTestMaps.GroundKind));
		var entity = Spawn(game, definition, V(80, 80));
		var collision = entity.Get<CollisionComponent>();
		Assert.False(collision.EnableCollision);
		Assert.Equal(29, collision.Radius);
	}

	[Fact]
	public void NegativeRadiiAreRejected()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new UnitDef("invalid") { Radius = -1 });
		Assert.Throws<ArgumentOutOfRangeException>(() => new CollisionComponent(true, -1));
	}

	[Theory]
	[InlineData(8, 8, false)]
	[InlineData(16, 32, true)]
	[InlineData(32, 16, true)]
	[InlineData(0, 32, true)]
	[InlineData(0, 0, false)]
	public void AvoidanceUsesTheSumOfBothRadii(int selfRadius, int otherRadius, bool shouldSteer)
	{
		var position = Steer(selfRadius, otherRadius);
		if (shouldSteer) Assert.True(position.Y < (Fixed64)160);
		else Assert.Equal((Fixed64)160, position.Y);
	}

	[Theory]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(false, false)]
	public void DisabledCollisionsDoNotCauseAvoidance(bool selfEnabled, bool otherEnabled)
	{
		Assert.Equal((Fixed64)160, Steer(16, 32, selfEnabled, otherEnabled).Y);
	}

	[Fact]
	public void StationaryCollidersWithoutVelocityAreAvoided()
	{
		Assert.True(Steer(16, 32, stationary: true).Y < (Fixed64)160);
	}

	[Fact]
	public void CoincidentCollidersProduceASeparationDirection()
	{
		Assert.NotEqual((Fixed64)160, Steer(16, 16, overlap: true).Y);
		Assert.Equal((Fixed64)160, Steer(0, 0, overlap: true).Y);
	}

	[Fact]
	public void A32PixelGapAdmitsRadius16ButRejectsRadius17()
	{
		var map = PathTestMaps.Build(11, 9,
			(x, y) => x == 5 && y != 4 ? PathTestMaps.CliffKind : PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		var small = FastUnit("small");
		small.Radius = 16;
		var large = FastUnit("large");
		large.Radius = 17;
		var first = Spawn(game, small, map.TileCenter(2, 4));
		var second = Spawn(game, large, map.TileCenter(2, 4));
		var goal = map.TileCenter(8, 4);
		Plan(game, first, goal);
		Plan(game, second, goal);
		Assert.NotEmpty(game.Pathfinding.Paths[new EntityHandle(first)].Waypoints);
		Assert.Empty(game.Pathfinding.Paths[new EntityHandle(second)].Waypoints);
	}

	[Fact]
	public void GoalAndFlowCacheRespectCollisionRadius()
	{
		var map = PathTestMaps.Build(9, 9,
			(x, _) => x == 5 ? PathTestMaps.CliffKind : PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		var state = game.Pathfinding;
		Assert.True(state.Cost(4, 4, PassageMask.Ground, 16, true) > 0);
		Assert.Equal(0, state.Cost(4, 4, PassageMask.Ground, 17, true));
		var goal = state.ClampGoal((4, 4), PassageMask.Ground, 17, true);
		Assert.NotEqual((4, 4), goal);
		Assert.True(state.Cost(goal.X, goal.Y, PassageMask.Ground, 17, true) > 0);
		var packed = FlowField.Pack(2, 4);
		var small = state.Fields.GetOrCreate(packed, PassageMask.Ground, 16);
		var large = state.Fields.GetOrCreate(packed, PassageMask.Ground, 17);
		Assert.NotSame(small, large);
		Assert.Same(small, state.Fields.GetOrCreate(packed, PassageMask.Ground, 16));
	}

	[Fact]
	public void DisabledCollisionIgnoresBuildingsButStillRespectsTerrain()
	{
		var map = PathTestMaps.Build(9, 3,
			(_, y) => y == 1 ? PathTestMaps.GroundKind : PathTestMaps.CliffKind);
		using var game = new GameWorld(map);
		Spawn(game, new BuildingDef("wall") { MaxHealth = 100 }, map.TileCenter(4, 1));
		var solid = Spawn(game, FastUnit("solid"), map.TileCenter(1, 1));
		var ghostDef = FastUnit("ghost");
		ghostDef.EnableCollision = false;
		ghostDef.Radius = 80;
		var ghost = Spawn(game, ghostDef, map.TileCenter(1, 1));
		var goal = map.TileCenter(7, 1);
		Plan(game, solid, goal);
		Plan(game, ghost, goal);
		Assert.Empty(game.Pathfinding.Paths[new EntityHandle(solid)].Waypoints);
		Assert.NotEmpty(game.Pathfinding.Paths[new EntityHandle(ghost)].Waypoints);
		Assert.Equal(0, game.Pathfinding.Cost(4, 0, PassageMask.Ground, 80, false));
	}

	[Fact]
	public void BuildingRadiusExpandsOccupancyAndDeletionRemovesTheWholeShape()
	{
		var map = PathTestMaps.Build(9, 9, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		var building = Spawn(game, new BuildingDef("wide") { MaxHealth = 100, Radius = 48 }, map.TileCenter(4, 4));
		Assert.Equal(48, building.Get<CollisionComponent>().Radius);
		var occupancy = game.Pathfinding.Occupancy;
		Assert.True(occupancy.IsOccupied(3, 4));
		Assert.True(occupancy.IsOccupied(4, 3));
		Assert.True(occupancy.IsOccupied(5, 5));
		Assert.False(occupancy.IsOccupied(2, 4));
		var id = game.IdOf(new EntityHandle(building));
		new DeleteEntityApplier().Apply(game.World, new DeleteEntityCommand(id));
		for (var y = 0; y < map.Height; y++)
		for (var x = 0; x < map.Width; x++)
			Assert.False(occupancy.IsOccupied(x, y));
	}

	[Fact]
	public void DisabledBuildingDoesNotCreateOccupancy()
	{
		var map = PathTestMaps.Build(9, 9, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		Spawn(game, new BuildingDef("ghost")
		{
			MaxHealth = 100, EnableCollision = false, Radius = 64, Collision = new Footprint(2, 2, 2, 2),
		}, map.TileCenter(4, 4));
		for (var y = 0; y < map.Height; y++)
		for (var x = 0; x < map.Width; x++)
			Assert.False(game.Pathfinding.Occupancy.IsOccupied(x, y));
	}

	[Fact]
	public void RadiusTooLargeForTheMapProducesNoPath()
	{
		var map = PathTestMaps.Build(1, 1, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		var definition = FastUnit("large");
		definition.Radius = 17;
		var entity = Spawn(game, definition, map.TileCenter(0, 0));
		Plan(game, entity, map.TileCenter(0, 0));
		Assert.False(game.Pathfinding.HasPath(new EntityHandle(entity)));
	}

	private Vector2d Steer(int radius, int otherRadius, bool selfEnabled = true, bool otherEnabled = true,
		bool stationary = false, bool overlap = false)
	{
		var map = PathTestMaps.Build(20, 20, (_, _) => PathTestMaps.GroundKind);
		using var game = new GameWorld(map);
		var definition = FastUnit("mover");
		definition.Radius = radius;
		definition.EnableCollision = selfEnabled;
		var entity = Spawn(game, definition, V(160, 160));
		EntityDef obstacle = stationary ? new BuildingDef("obstacle") { MaxHealth = 100 } : FastUnit("obstacle");
		obstacle.Radius = otherRadius;
		obstacle.EnableCollision = otherEnabled;
		Spawn(game, obstacle, overlap ? V(160, 160) : V(205, 178));
		game.Pathfinding.SetPath(new EntityHandle(entity), [V(500, 160)], FlowField.Pack(15, 5));
		Movement.Operation(game.World, (Fixed64)0.1);
		return entity.Get<TransformComponent>().Position;
	}

	private static Entity Spawn(GameWorld game, EntityDef definition, Vector2d position)
	{
		new SpawnEntityApplier().Apply(game.World, new SpawnEntityCommand(definition, new TransformComponent { Position = position }, 0));
		Assert.True(game.Units.TryGet(new UnitId(game.Units.LastIssuedId), out var entity));
		return entity;
	}

	private void Plan(GameWorld game, Entity entity, Vector2d goal)
		=> Pathfinding.Apply(game.World, new WaypointCommand(game.Select(new EntityHandle(entity)), WaypointType.Move,
			WaypointAddType.Replace, new GroundTarget(goal)));
}
