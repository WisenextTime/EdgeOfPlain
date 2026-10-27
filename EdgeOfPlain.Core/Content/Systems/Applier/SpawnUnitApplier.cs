using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Components;

namespace EdgeOfPlain.Core.Content.Systems.Applier;

public sealed class SpawnUnitApplier : ICommandApplier<SpawnUnitCommand>
{
	public void Apply(World world, in SpawnUnitCommand command)
	{
		var entity = ComponentManager.CreateEntity(world, command.Definition);
		entity.Set(command.Transform);
	}
}