using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Game.Components;

namespace EdgeOfPlain.Core.Game.Commands;

public readonly struct SpawnUnitCommand(UnitDef definition, TransformComponent transform) : ICommand
{
	public readonly UnitDef Definition = definition;
	public readonly TransformComponent Transform = transform;
}

public sealed class SpawnUnitApplier : ICommandApplier<SpawnUnitCommand>
{
	public void Apply(World world, in SpawnUnitCommand command)
	{
		var entity = ComponentManager.CreateEntity(world, command.Definition);
		entity.Set(command.Transform);
	}
}
