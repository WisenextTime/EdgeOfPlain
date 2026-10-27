using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Game.Commands;

namespace EdgeOfPlain.Core.Content.Systems.Commands;

public readonly struct SpawnUnitCommand(UnitDef definition, TransformComponent transform) : ICommand
{
	public readonly UnitDef Definition = definition;
	public readonly TransformComponent Transform = transform;
}