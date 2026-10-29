using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;

namespace EdgeOfPlain.Core.Content.Components;

public struct MoveTypeComponent(PassageMask type)
{
	public PassageMask Type = type;
	public static MoveTypeComponent CreateComponent(EntityDef def) => new(def.MoveType);
}
