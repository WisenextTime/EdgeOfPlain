using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;

namespace EdgeOfPlain.Core.Content.Systems;

public sealed class UnitDiedApplier : ICommandApplier<UnitDiedCommand>
{
	public void Apply(World world, in UnitDiedCommand command)
	{
		var entity = command.Entity.Value;
		if (entity.IsAlive()) world.Destroy(entity);
	}
}
