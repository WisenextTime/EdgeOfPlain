using System;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems.Applier;
using EdgeOfPlain.Core.Content.Systems.PathFinding;
using EdgeOfPlain.Core.Content.Systems.Queues;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Game.Components;

namespace EdgeOfPlain.Core.Content;

public class Base : Mod
{
	public override ModInfo ModInfo { get; } = new("base", new Version(1, 0, 0), "WisenextTime", "The base pack of game.");

	public override Type[]? RegisterTypes { get; protected set; } =
	[
		typeof(TerrainDef),
		typeof(TilesetDef),
		typeof(TileRendererDef),
		typeof(UnitDef),
		typeof(BuildingDef),
	];

	public override void InitMod(ModContext context)
	{
		context.AddComponent(HealthComponent.CreateComponent);
		context.AddComponent(TransformComponent.CreateComponent);
		context.AddComponent(TeamComponent.CreateComponent);
		context.AddComponent(MoveTypeComponent.CreateComponent);
		context.AddComponent(VelocityComponent.CreateComponent, ComponentScope<UnitDef>.ForDefType());

		context.AddQueue(new DeathQueueSystem());

		context.AddApplier(new SpawnEntityApplier());
		context.AddApplier(new DeleteEntityApplier());

		var pathfinding = new PathfindingSystem();
		context.AddApplier(pathfinding);

		var movement = new MovementSystem(pathfinding);
		context.AddFrameSystem(movement);
	}
}