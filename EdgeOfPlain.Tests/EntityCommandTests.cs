using System;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Content.Systems.Applier;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Components;
using EdgeOfPlain.Core.Net;
using FixedMathSharp;
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

		var def = new UnitDef("testUnit") { MaxHealth = 40, InitHealth = (Fixed64)0.5 };
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
		var id = UnitRegistry.GetOrCreate(world).Register(handle.Value);
		new DeleteEntityApplier().Apply(world, new DeleteEntityCommand(id));

		Assert.Equal(0, world.Size);
	}

	[Fact]
	public void DeleteApplierIgnoresUnknownIdentifiers()
	{
		using var world = World.Create();

		var def = new UnitDef("testUnit") { MaxHealth = 10 };
		new SpawnEntityApplier().Apply(world, new SpawnEntityCommand(def, new TransformComponent(), 0));

		new DeleteEntityApplier().Apply(world, new DeleteEntityCommand(new UnitId(999)));

		Assert.Equal(1, world.Size);
	}

	[Fact]
	public void DeleteApplierIgnoresStaleEntities()
	{
		using var world = World.Create();

		var def = new UnitDef("testUnit") { MaxHealth = 10 };
		new SpawnEntityApplier().Apply(world, new SpawnEntityCommand(def, new TransformComponent(), 0));

		var entity = SingleEntity(world);
		var id = UnitRegistry.GetOrCreate(world).Register(entity);
		world.Destroy(entity);

		new DeleteEntityApplier().Apply(world, new DeleteEntityCommand(id));

		Assert.Equal(0, world.Size);
	}

	[Fact]
	public void SpawnAndDeleteCommandsRoundTripThroughNetworkFrames()
	{
		CommandRegistry.Reset();
		ContentStore.Reset();
		try
		{
			var def = new UnitDef("networkUnit") { SourceMod = TestMods.Base, MaxHealth = 10 };
			ContentStore.Add(def);
			CommandRegistry.RegisterApplier(new SpawnEntityApplier());
			CommandRegistry.RegisterApplier(new DeleteEntityApplier());
			CommandRegistry.Seal();
			Assert.Equal(2, CommandRegistry.NetworkSignatures.Count);

			using var first = new GameWorld(GameMap.Build(new Map(2, 2)));
			using var second = new GameWorld(GameMap.Build(new Map(2, 2)));
			var input = new NetworkCommandInput();
			var position = new Vector2d((Fixed64)3, (Fixed64)4);
			input.Add(new SpawnEntityCommand(def,
				new TransformComponent { Position = position, Rotation = (Fixed64)1.25 }, 0));
			var spawn = new TurnFrame(1, [new PlayerCommands(0, input.Flush())]);
			NetworkCommands.Enqueue(spawn, first.Commands);
			NetworkCommands.Enqueue(spawn, second.Commands);
			first.Step((Fixed64)0.1);
			second.Step((Fixed64)0.1);
			Assert.Equal(position, SingleEntity(first.World).Get<TransformComponent>().Position);
			Assert.Equal((Fixed64)1.25, SingleEntity(second.World).Get<TransformComponent>().Rotation);

			input.Add(new DeleteEntityCommand(UnitIdFor(first, SingleEntity(first.World))));
			var delete = new TurnFrame(2, [new PlayerCommands(0, input.Flush())]);
			NetworkCommands.Enqueue(delete, first.Commands);
			NetworkCommands.Enqueue(delete, second.Commands);
			first.Step((Fixed64)0.1);
			second.Step((Fixed64)0.1);
			Assert.Equal(0, first.World.Size);
			Assert.Equal(0, second.World.Size);
		}
		finally
		{
			ContentStore.Reset();
			CommandRegistry.Reset();
		}
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
	}

	private static UnitId UnitIdFor(GameWorld game, Entity entity) =>
		game.TryGetId(new EntityHandle(entity), out var id) ? id : game.IdOf(new EntityHandle(entity));

	private static Entity SingleEntity(World world)
	{
		var description = new QueryDescription().WithAll<TransformComponent>();

		Assert.Equal(1, world.CountEntities(in description));

		Span<Entity> entities = stackalloc Entity[1];
		world.GetEntities(in description, entities);

		return entities[0];
	}
}