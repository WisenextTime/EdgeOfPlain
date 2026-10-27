using System;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Content.Systems.Applier;
using EdgeOfPlain.Core.Content.Systems.Queues;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;

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
	];

	public override void InitMod(ModContext context)
	{
		context.AddComponent(HealthComponent.CreateComponent);
		context.AddComponent(TransformComponent.CreateComponent);

		context.AddQueue(new DeathQueueSystem());

		context.AddApplier(new SpawnUnitApplier());
		context.AddApplier(new DeleteUnitApplier());
		context.AddApplier(new UnitDiedApplier());
	}
}