using FixedMathSharp;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

public sealed class Fixed64Converter : TomlConverter<Fixed64>
{
	public override Fixed64 Read(TomlReader reader) => Fixed64.FromDecimal(reader.GetDecimal());

	public override void Write(TomlWriter writer, Fixed64 value)
	{
		var integer = (long)value;
		if ((Fixed64)integer == value)
			writer.WriteIntegerValue(integer);
		else
			writer.WriteFloatValue((double)value);
	}
}