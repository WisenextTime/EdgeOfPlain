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
using FixedMathSharp;
using FixedMathSharp.Geometry;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class EntityCommandTests
{
	public EntityCommandTests()
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
			Position = new Vector2d((Fixed64)3, (Fixed64)4),
			Rotation = (Fixed64)1.25,
		};

		new SpawnEntityApplier().Apply(world, new SpawnEntityCommand(def, transform, 0));

		Assert.Equal(1, world.Size);

		var entity = SingleEntity(world);

		Assert.Equal(new Vector2d((Fixed64)3, (Fixed64)4), entity.Get<TransformComponent>().Position);
		Assert.Equal((Fixed64)1.25, entity.Get<TransformComponent>().Rotation);
		Assert.Equal(40, entity.Get<HealthComponent>().MaxHealth);
		Assert.Equal(20, entity.Get<HealthComponent>().Health);
	}

	[Fact]
	public void SpawnApplierCreatesBuildingEntitiesFromBuildingDef()
	{
		using var world = World.Create();

		var def = new BuildingDef("tower") { MaxHealth = 100 };
		new SpawnEntityApplier().Apply(world, new SpawnEntityCommand(def, new TransformComponent(), 0));

		Assert.Equal(1, world.Size);

		var entity = SingleEntity(world);

		Assert.Equal(100, entity.Get<HealthComponent>().MaxHealth);
	}

	[Fact]
	public void DeleteApplierDestroysTheEntity()
	{
		using var world = World.Create();

		var def = new UnitDef("testUnit") { MaxHealth = 10 };
		new SpawnEntityApplier().Apply(world, new SpawnEntityCommand(def, new TransformComponent(), 0));

		var handle = new EntityHandle(SingleEntity(world));
		new DeleteEntityApplier().Apply(world, new DeleteEntityCommand(handle));

		Assert.Equal(0, world.Size);
	}

	[Fact]
	public void DeleteApplierIgnoresStaleEntities()
	{
		using var world = World.Create();

		var def = new UnitDef("testUnit") { MaxHealth = 10 };
		new SpawnEntityApplier().Apply(world, new SpawnEntityCommand(def, new TransformComponent(), 0));

		var entity = SingleEntity(world);
		world.Destroy(entity);

		new DeleteEntityApplier().Apply(world, new DeleteEntityCommand(new EntityHandle(entity)));

		Assert.Equal(0, world.Size);
	}

	[Fact]
	public void GameWorldAppliesCommandsThroughItsDispatcher()
	{
		using var game = new GameWorld(GameMap.Build(new Map(2, 2)));
		game.Dispatcher.Register(new SpawnEntityApplier());

		var def = new UnitDef("testUnit") { MaxHealth = 10 };

		game.Commands.Enqueue(new SpawnEntityCommand(
			def,
			new TransformComponent { Position = new Vector2d((Fixed64)1, (Fixed64)2) },
			0));

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
