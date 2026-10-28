using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Game.Commands;

namespace EdgeOfPlain.Core.Content.Systems.Applier;

public sealed class EntityDiedApplier : ICommandApplier<EntityDiedCommand>
{
	public void Apply(World world, in EntityDiedCommand command)
	{
		var entity = command.Entity.Value;
		if (entity.IsAlive()) world.Destroy(entity);
	}
}
