using Arch.Core;
using EdgeOfPlain.Core.Content.Systems.Commands;
using EdgeOfPlain.Core.Game.Commands;

namespace EdgeOfPlain.Core.Content.Systems.Applier;

public sealed class DeleteUnitApplier : ICommandApplier<DeleteUnitCommand>
{
	public void Apply(World world, in DeleteUnitCommand command)
	{
		var entity = command.Target.Value;
		if (world.IsAlive(entity)) world.Destroy(entity);
	}
}