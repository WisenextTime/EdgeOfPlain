using Arch.Core;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.PathFinding;

namespace EdgeOfPlain.Core.Content.Systems.Applier;

public sealed class DeleteEntityApplier : ICommandApplier<DeleteEntityCommand>
{
	public void Apply(World world, in DeleteEntityCommand command)
	{
		var units = UnitRegistry.GetOrCreate(world);
		if (!units.TryResolve(world, command.Target, out var entity)) return;

		var handle = new EntityHandle(entity);
		var pathfinding = WorldPathfinding.TryGet(world);
		pathfinding?.Occupancy.Remove(handle);
		pathfinding?.ClearPath(handle);

		units.Unregister(entity);
		world.Destroy(entity);
	}
}