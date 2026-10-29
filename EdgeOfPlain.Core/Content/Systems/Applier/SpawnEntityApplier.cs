using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Components;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Content.Systems.Applier;

public sealed class SpawnEntityApplier : ICommandApplier<SpawnEntityCommand>
{
	public void Apply(World world, in SpawnEntityCommand command)
	{
		var entity = ComponentManager.CreateEntity(world, command.Definition);
		entity.Set(command.Transform);
		entity.Set(new TeamComponent(command.Team));
		entity.Set(MoveTypeComponent.CreateComponent(command.Definition));

		if (command.Definition is not BuildingDef building) return;

		var pathfinding = WorldPathfinding.TryGet(world);
		if (pathfinding is null) return;

		var tile = pathfinding.Map.WorldToTile(command.Transform.Position);
		pathfinding.Occupancy.Add(
			new EntityHandle(entity),
			building.Collision,
			tile.X,
			tile.Y,
			pathfinding.Map);
	}
}
