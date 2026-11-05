using System;
using Arch.Core;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Content.Systems.Applier;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Components;
using FixedMathSharp;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class GameCoreTests
{
	public GameCoreTests()
	{
		if (ComponentManager.IsSealed) return;

		ComponentManager.StageComponent(HealthComponent.CreateComponent, "test");
		ComponentManager.StageComponent(TransformComponent.CreateComponent, "test");
		ComponentManager.Seal();
	}

	[Fact]
	public void FirstWorldBecomesPrimary()
	{
		using var core = new GameCore();
		var first = core.CreateWorld(new WorldKey(1), GameMap.Build(new Map(2, 2)));
		var second = core.CreateWorld(new WorldKey(2), GameMap.Build(new Map(2, 2)));

		Assert.Equal(2, core.Count);
		Assert.Equal(new WorldKey(1), core.PrimaryKey);
		Assert.Same(first, core.PrimaryWorld);
		Assert.Same(second, core.GetWorld(new WorldKey(2)));
		Assert.Equal(new WorldKey(2), core.KeyOf(second));
	}

	[Fact]
	public void DuplicateOrInvalidKeysAreRejected()
	{
		using var core = new GameCore();
		var map = GameMap.Build(new Map(2, 2));
		core.CreateWorld(new WorldKey(1), map);

		Assert.Throws<InvalidOperationException>(() => core.CreateWorld(new WorldKey(1), map));
		Assert.Throws<ArgumentOutOfRangeException>(() => core.CreateWorld(WorldKey.Invalid, map));
		Assert.Throws<InvalidOperationException>(() => core.GetWorld(new WorldKey(9)));
	}

	[Fact]
	public void CommandsReachTheWorldTheyName()
	{
		using var core = new GameCore();
		var first = core.CreateWorld(new WorldKey(1), GameMap.Build(new Map(4, 4)));
		var second = core.CreateWorld(new WorldKey(2), GameMap.Build(new Map(4, 4)));
		first.Dispatcher.Register(new SpawnEntityApplier());
		second.Dispatcher.Register(new SpawnEntityApplier());

		var def = new UnitDef("coreUnit") { MaxHealth = 10 };
		var transform = new TransformComponent { Position = new Vector2d((Fixed64)1, (Fixed64)1) };

		core.Enqueue(new SpawnEntityCommand(def, transform, 0) { World = new WorldKey(2) });
		core.Step();

		Assert.Equal(0, first.World.Size);
		Assert.Equal(1, second.World.Size);
	}

	[Fact]
	public void CommandsWithoutAWorldFallBackToPrimary()
	{
		using var core = new GameCore();
		var first = core.CreateWorld(new WorldKey(1), GameMap.Build(new Map(4, 4)));
		core.CreateWorld(new WorldKey(2), GameMap.Build(new Map(4, 4)));
		first.Dispatcher.Register(new SpawnEntityApplier());

		var def = new UnitDef("coreUnit") { MaxHealth = 10 };
		core.Enqueue(new SpawnEntityCommand(def, new TransformComponent(), 0));
		core.Step();

		Assert.Equal(1, first.World.Size);
	}

	[Fact]
	public void UnknownWorldIsRejected()
	{
		using var core = new GameCore();
		core.CreateWorld(new WorldKey(1), GameMap.Build(new Map(2, 2)));

		var def = new UnitDef("coreUnit") { MaxHealth = 10 };
		var command = new SpawnEntityCommand(def, new TransformComponent(), 0) { World = new WorldKey(7) };

		Assert.Throws<InvalidOperationException>(() => core.Enqueue(command));
	}

	[Fact]
	public void RegistryAssignsIdentifiersInRegistrationOrder()
	{
		using var world = World.Create();
		var registry = UnitRegistry.Create(world);
		var first = world.Create(new TransformComponent());
		var second = world.Create(new TransformComponent());

		var a = registry.Register(first);
		var b = registry.Register(second);

		Assert.Equal(new UnitId(1), a);
		Assert.Equal(new UnitId(2), b);
		Assert.Equal(a, registry.Register(first));
		Assert.Equal(2, registry.Count);
		Assert.True(registry.TryGet(a, out var resolved));
		Assert.Equal(first, resolved);
	}

	[Fact]
	public void RegistryDropsStaleEntriesOnResolve()
	{
		using var world = World.Create();
		var registry = UnitRegistry.Create(world);
		var entity = world.Create(new TransformComponent());
		var id = registry.Register(entity);
		world.Destroy(entity);

		Assert.False(registry.TryResolve(world, id, out _));
		Assert.Equal(0, registry.Count);
	}

	[Fact]
	public void RegistryUnregistersExplicitly()
	{
		using var world = World.Create();
		var registry = UnitRegistry.Create(world);
		var entity = world.Create(new TransformComponent());
		var id = registry.Register(entity);

		registry.Unregister(entity);

		Assert.False(registry.TryGet(id, out _));
		Assert.False(registry.TryGet(entity, out _));
	}
}