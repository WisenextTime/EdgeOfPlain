using Arch.Core;
using EdgeOfPlain.Core.Game.Commands;

namespace EdgeOfPlain.Core.Content.Systems.Applier;

public sealed class DeleteEntityApplier : ICommandApplier<DeleteEntityCommand>
{
	public void Apply(World world, in DeleteEntityCommand command)
	{
		var entity = command.Target.Value;
		if (world.IsAlive(entity)) world.Destroy(entity);
	}
}
