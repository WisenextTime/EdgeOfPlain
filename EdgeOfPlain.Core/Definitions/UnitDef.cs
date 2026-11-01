using FixedMathSharp;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

[ContentTypeName("unit")]
public class UnitDef(string id) : EntityDef(id)
{
	[TomlConverter(typeof(Fixed64Converter))]
	public Fixed64 MaxVelocity { get; set; } = (Fixed64)120;

	[TomlConverter(typeof(Fixed64Converter))]
	public Fixed64 Acceleration { get; set; } = (Fixed64)70;

	[TomlConverter(typeof(Fixed64Converter))]
	public Fixed64 RotationalVelocity { get; set; } = (Fixed64)5;
	public bool AlignBeforeTurn { get; set; }
	
}
