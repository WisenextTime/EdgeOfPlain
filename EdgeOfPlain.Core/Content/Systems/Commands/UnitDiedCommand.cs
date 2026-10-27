using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Content.Systems.Commands;

public readonly struct UnitDiedCommand(EntityHandle entity) : ICommand
{
	public readonly EntityHandle Entity = entity;

}
