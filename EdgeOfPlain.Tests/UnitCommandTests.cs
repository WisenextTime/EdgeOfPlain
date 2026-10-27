using System;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Content.Systems.Applier;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Components;
using EdgeOfPlain.Core.Game.Systems;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class UnitCommandTests
{
	public UnitCommandTests()
	{
		if (ComponentManager.IsSealed) return;

		ComponentManager.StageComponent(HealthComponent.CreateComponent, "test");
		ComponentManager.StageComponent(TransformComponent.CreateComponent, "test");
		ComponentManager.Seal();
	}

	[Fact]
	public void SpawnApplierCreatesEntityWithTheCommandTransform()
	{
		using var world = World.Create();

		var def = new UnitDef("testUnit") { MaxHealth = 40, InitHealth = 0.5f };
		var transform = new TransformComponent
		{
			Position = new Vector2(3f, 4f),
			Rotation = 1.25f,
		};

		new SpawnUnitApplier().Apply(world, new SpawnUnitCommand(def, transform));

		Assert.Equal(1, world.Size);

		var entity = SingleEntity(world);

		Assert.Equal(new Vector2(3f, 4f), entity.Get<TransformComponent>().Position);
		Assert.Equal(1.25f, entity.Get<TransformComponent>().Rotation);
		Assert.Equal(40, entity.Get<HealthComponent>().MaxHealth);
		Assert.Equal(20, entity.Get<HealthComponent>().Health);
	}

	[Fact]
	public void DeleteApplierDestroysTheEntity()
	{
		using var world = World.Create();

		var def = new UnitDef("testUnit") { MaxHealth = 10 };
		new SpawnUnitApplier().Apply(world, new SpawnUnitCommand(def, new TransformComponent()));

		var handle = new EntityHandle(SingleEntity(world));
		new DeleteUnitApplier().Apply(world, new DeleteUnitCommand(handle));

		Assert.Equal(0, world.Size);
	}

	[Fact]
	public void DeleteApplierIgnoresStaleEntities()
	{
		using var world = World.Create();

		var def = new UnitDef("testUnit") { MaxHealth = 10 };
		new SpawnUnitApplier().Apply(world, new SpawnUnitCommand(def, new TransformComponent()));

		var entity = SingleEntity(world);
		world.Destroy(entity);

		new DeleteUnitApplier().Apply(world, new DeleteUnitCommand(new EntityHandle(entity)));

		Assert.Equal(0, world.Size);
	}

	[Fact]
	public void GameWorldAppliesCommandsThroughItsDispatcher()
	{
		using var game = new GameWorld(GameMap.Build(new Map(2, 2)));
		game.Dispatcher.Register(new SpawnUnitApplier());

		var def = new UnitDef("testUnit") { MaxHealth = 10 };

		game.Commands.Enqueue(new SpawnUnitCommand(
			def,
			new TransformComponent { Position = new Vector2(1f, 2f) }));

		Assert.Equal(1, game.ApplyCommands());
		Assert.Equal(1, game.QueueView().Count);
	}

	private static Entity SingleEntity(World world)
	{
		var description = new QueryDescription().WithAll<TransformComponent>();

		Assert.Equal(1, world.CountEntities(in description));

		Span<Entity> entities = stackalloc Entity[1];
		world.GetEntities(in description, entities);

		return entities[0];
	}
}
