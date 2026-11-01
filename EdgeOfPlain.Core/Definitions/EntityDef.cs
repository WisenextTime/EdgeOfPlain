using EdgeOfPlain.Core.Definitions.Worlds;
using FixedMathSharp;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

public abstract class EntityDef(string id) : ContentDef(id)
{
	[TomlRequired] public int MaxHealth { get; set; }
	[TomlConverter(typeof(Fixed64Converter))] public Fixed64 InitHealth { get; set; } = (Fixed64)1;
	[TomlRequired] public string Texture { get; set; } = "";

	public PassageMask MoveType { get; set; } = PassageMask.Ground;
}
