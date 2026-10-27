using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Content.Systems.Commands;

public readonly struct DeleteUnitCommand(EntityHandle target) : ICommand
{
	internal readonly EntityHandle Target = target;
}