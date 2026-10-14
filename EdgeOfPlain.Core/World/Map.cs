using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.World;

[method:TomlConstructor]
public class Map(int width, int height)
{
	public static Version DefaultMapVersion => new(1, 0, 0);
	public Version MapVersion { get; set; } = DefaultMapVersion;
	[TomlConverter(typeof(TileGridConverter))]
	public ushort[,] Tiles { get; set; } = new ushort[width, height];
	public List<string> Tilesets { get; set; } = [];
	public int Width => Tiles.GetLength(0);
	public int Height => Tiles.GetLength(1);
}

public sealed class TileGridConverter : TomlConverter<ushort[,]>
{
	public override ushort[,] Read(TomlReader reader)
	{
		var source = reader.GetString();
		var decompressed = Decompress(source);
		var rows = BinaryPrimitives.ReadInt32LittleEndian(decompressed.AsSpan(0));
		var cols = BinaryPrimitives.ReadInt32LittleEndian(decompressed.AsSpan(4));
		var value = new ushort[rows, cols];
		Buffer.BlockCopy(decompressed, 8, value, 0, rows * cols);
		return value;
	}

	public override void Write(TomlWriter writer, ushort[,] value)
	{
		var rows = value.GetLength(0);
		var cols = value.GetLength(1);
		var raw = new byte[rows * cols * sizeof(short) + 2 * sizeof(int)];
		BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(0), rows);
		BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(4), cols);
		Buffer.BlockCopy(value, 0, raw, 8, rows * cols);
		var compressed = Compress(raw);
		writer.WriteStringValue(compressed);
	}

	private static string Compress(byte[] data)
	{
		using var output = new MemoryStream();
		using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, true))
			deflate.Write(data, 0, data.Length);
		return Convert.ToBase64String(output.ToArray());
	}

	private static byte[] Decompress(string data)
	{
		using var input = new MemoryStream(Convert.FromBase64String(data));
		using var deflate = new DeflateStream(input, CompressionMode.Decompress);
		using var output = new MemoryStream();
		deflate.CopyTo(output);
		return output.ToArray();
	}
}
