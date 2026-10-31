using System;
using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems.Applier;
using EdgeOfPlain.Core.Content.Systems.PathFinding;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Components;
using EdgeOfPlain.Core.Game.Systems;
using Xunit;

namespace EdgeOfPlain.Tests;

public abstract class PathSystemTestBase : IDisposable
{
	protected readonly PathfindingSystem Pathfinding = new();
	protected readonly MovementSystem Movement;

	public PathSystemTestBase()
	{
		Movement = new MovementSystem(Pathfinding);
		ContentStore.Reset();
		ComponentManager.Reset();

		ComponentManager.StageComponent(HealthComponent.CreateComponent, "test");
		ComponentManager.StageComponent(TransformComponent.CreateComponent, "test");
		ComponentManager.StageComponent(TeamComponent.CreateComponent, "test");
		ComponentManager.StageComponent(MoveTypeComponent.CreateComponent, "test");
		ComponentManager.StageComponent(
			VelocityComponent.CreateComponent,
			ComponentScope<UnitDef>.ForDefType(),
			"test");
		ComponentManager.Seal();
	}

	public void Dispose()
	{
		CommandRegistry.Reset();
		ContentStore.Reset();
	}

	protected void RegisterSystems(GameWorld game)
	{
		game.Dispatcher.Register(new SpawnEntityApplier());
		game.Dispatcher.Register(new DeleteEntityApplier());
		game.Dispatcher.Register(Pathfinding);
		CommandRegistry.RegisterFrameSystem(Movement);
	}

	protected static UnitDef FastUnit(string id) => new UnitDef(id)
	{
		MaxHealth = 10,
		MaxVelocity = 60f,
		Acceleration = 100f,
		RotationalVelocity = 10f,
	};

	protected static void StepUntilArrived(GameWorld game, EntityHandle handle, int maxSteps = 300)
	{
		for (var i = 0; i < maxSteps && game.Pathfinding.HasPath(handle); i++)
			game.Step(0.1f);
	}

	protected static Entity SingleEntity(GameWorld game)
	{
		var description = new QueryDescription().WithAll<TransformComponent>();

		Assert.Equal(1, game.CollectEntities(description, new EntityHandle[1]));

		Span<Entity> entities = stackalloc Entity[1];
		game.World.GetEntities(in description, entities);

		return entities[0];
	}

	protected static List<Entity> UnitsNear(GameWorld game, Vector2 near)
	{
		var result = new List<Entity>();
		var description = new QueryDescription().WithAll<TransformComponent, VelocityComponent>();

		game.World.Query(description, (Entity entity, ref TransformComponent _, ref VelocityComponent _) =>
		{
			if (Vector2.DistanceSquared(entity.Get<TransformComponent>().Position, near) < 100f)
				result.Add(entity);
		});

		return result;
	}
}