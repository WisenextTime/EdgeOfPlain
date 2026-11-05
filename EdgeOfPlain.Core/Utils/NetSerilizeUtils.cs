using System.Globalization;
using FixedMathSharp;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Utils;

public static class NetSerilizeUtils
{
	public static void PutFixed(NetDataWriter writer, Fixed64 value) =>
		writer.Put(long.Parse(value.ToRawString(), CultureInfo.InvariantCulture));

	public static Fixed64 ReadFixed(NetDataReader reader)
		=> Fixed64.FromRaw(reader.GetLong());
}