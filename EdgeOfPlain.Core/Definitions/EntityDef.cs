using EdgeOfPlain.Core.Definitions.Worlds;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

public abstract class EntityDef(string id) : ContentDef(id)
{
	[TomlRequired] public int MaxHealth { get; set; }
	public float InitHealth { get; set; } = 1f;
	[TomlRequired] public string Texture { get; set; } = "";

	public PassageMask MoveType { get; set; } = PassageMask.Ground;
}
