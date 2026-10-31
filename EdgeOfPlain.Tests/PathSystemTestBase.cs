using System;
using System.Collections.Generic;
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
using FixedMathSharp;
using FixedMathSharp.Geometry;
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

	protected static Vector2d V(float x, float y) => new((Fixed64)x, (Fixed64)y);

	protected static void StepUntilArrived(GameWorld game, EntityHandle handle, int maxSteps = 300)
	{
		for (var i = 0; i < maxSteps && game.Pathfinding.HasPath(handle); i++)
			game.Step((Fixed64)0.1);
	}

	protected static Entity SingleEntity(GameWorld game)
	{
		var description = new QueryDescription().WithAll<TransformComponent>();

		Assert.Equal(1, game.CollectEntities(description, new EntityHandle[1]));

		Span<Entity> entities = stackalloc Entity[1];
		game.World.GetEntities(in description, entities);

		return entities[0];
	}

	protected static List<Entity> UnitsNear(GameWorld game, Vector2d near)
	{
		var result = new List<Entity>();
		var description = new QueryDescription().WithAll<TransformComponent, VelocityComponent>();

		game.World.Query(description, (Entity entity, ref TransformComponent _, ref VelocityComponent _) =>
		{
			if ((entity.Get<TransformComponent>().Position - near).MagnitudeSquared < (Fixed64)100)
				result.Add(entity);
		});

		return result;
	}
}