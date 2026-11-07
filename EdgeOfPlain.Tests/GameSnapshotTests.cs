using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Snapshots;
using FixedMathSharp;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class GameSnapshotTests
{
	[Fact]
	public async Task SnapshotPreservesFixedValuesUnitSequencePathsAndOccupancy()
	{
		using var source = new GameCore();
		var world = source.CreateWorld(WorldKey.Primary, GameMap.Build(new Map(4, 4)));
		var position = new Vector2d(Fixed64.FromRaw(123456789), Fixed64.FromRaw(-987654321));
		var entity = world.World.Create(new TransformComponent { Position = position, Rotation = Fixed64.FromRaw(33) },
			new VelocityComponent { Velocity = Fixed64.FromRaw(42), AlignBeforeTurn = true }, new CollisionComponent(false, 42));
		var id = world.Units.Register(entity);
		var deleted = world.World.Create(new SyncValue());
		var removedId = world.Units.Register(deleted);
		world.Units.Unregister(deleted);
		world.World.Destroy(deleted);
		var handle = new EntityHandle(entity);
		world.Pathfinding.SetPath(handle, [position, Vector2d.One], 123);
		world.Pathfinding.Paths[handle].Index = 1;
		world.Pathfinding.Paths[handle].RepathAttempts = 2;
		world.Pathfinding.Occupancy.Restore(new Dictionary<EntityHandle, long[]> { [handle] = [0, 1] }, 9);
		source.Pause();
		var bytes = await GameSnapshot.CaptureAsync(source, TestContext.Current.CancellationToken);
		var snapshot = await GameSnapshot.DecodeAsync(bytes, TestContext.Current.CancellationToken);
		using var target = new GameCore();
		var retained = target.CreateWorld(WorldKey.Primary, GameMap.Build(new Map(1, 1)));
		target.Pause();
		snapshot.Apply(target);
		Assert.Same(retained, target.PrimaryWorld);
		Assert.True(retained.TryResolve(id, out var restored));
		Assert.Equal(position, retained.World.Get<TransformComponent>(restored.Value).Position);
		Assert.Equal(Fixed64.FromRaw(42), retained.World.Get<VelocityComponent>(restored.Value).Velocity);
		Assert.True(retained.World.Get<VelocityComponent>(restored.Value).AlignBeforeTurn);
		Assert.False(retained.World.Get<CollisionComponent>(restored.Value).EnableCollision);
		Assert.Equal(42, retained.World.Get<CollisionComponent>(restored.Value).Radius);
		var path = retained.Pathfinding.Paths[restored];
		Assert.Equal(1, path.Index);
		Assert.Equal(2, path.RepathAttempts);
		Assert.Equal(123, path.TargetTile);
		Assert.True(retained.Pathfinding.Occupancy.IsOccupied(0, 0));
		Assert.True(retained.Pathfinding.Occupancy.IsOccupied(1, 0));
		Assert.Equal(9, retained.Pathfinding.Occupancy.Version);
		var newId = retained.Units.Register(retained.World.Create(new SyncValue()));
		Assert.Equal(removedId.Value + 1, newId.Value);
		Assert.True(target.IsPaused);
	}

	[Fact]
	public async Task CaptureRequiresPausedWorlds()
	{
		using var core = new GameCore();
		core.CreateWorld(WorldKey.Primary, GameMap.Build(new Map(1, 1)));
		await Assert.ThrowsAsync<InvalidOperationException>(() => GameSnapshot.CaptureAsync(core, TestContext.Current.CancellationToken));
	}
}
