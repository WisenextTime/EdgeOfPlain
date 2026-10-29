using Arch.Core;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.PathFinding;

namespace EdgeOfPlain.Core.Content.Systems.Applier;

public sealed class DeleteEntityApplier : ICommandApplier<DeleteEntityCommand>
{
	public void Apply(World world, in DeleteEntityCommand command)
	{
		var pathfinding = WorldPathfinding.TryGet(world);
		pathfinding?.Occupancy.Remove(command.Target);
		pathfinding?.ClearPath(command.Target);

		var entity = command.Target.Value;
		if (world.IsAlive(entity)) world.Destroy(entity);
	}
}
