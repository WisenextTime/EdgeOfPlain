using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Game.Commands;

public readonly struct DeleteUnitCommand(EntityHandle target) : ICommand
{
	internal readonly EntityHandle Target = target;
}

public sealed class DeleteUnitApplier : ICommandApplier<DeleteUnitCommand>
{
	public void Apply(World world, in DeleteUnitCommand command)
	{
		var entity = command.Target.Value;
		if (world.IsAlive(entity)) world.Destroy(entity);
	}
}
