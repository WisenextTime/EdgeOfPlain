using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Components;

namespace EdgeOfPlain.Core.Content.Systems.Applier;

public sealed class SpawnEntityApplier : ICommandApplier<SpawnEntityCommand>
{
	public void Apply(World world, in SpawnEntityCommand command)
	{
		var entity = ComponentManager.CreateEntity(world, command.Definition);
		entity.Set(command.Transform);
		entity.Set(new TeamComponent(command.Team));
	}
}
