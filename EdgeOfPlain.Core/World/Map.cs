using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.World;

public class Map
{
	[TomlConverter(typeof(TileGridConverter))]
	public int[,] Tiles { get; set; } = new int[0, 0];
	public List<string> Tilesets { get; set; } = [];

	public int Width => Tiles.GetLength(0);

	public int Height => Tiles.GetLength(1);
}

public sealed class TileGridConverter : TomlConverter<int[,]>
{
	public override int[,] Read(TomlReader reader)
	{
		var compressed = Convert.FromBase64String(reader.GetString());
		var bytes = Decompress(compressed);
		var width = BinaryPrimitives.ReadInt32LittleEndian(bytes);
		var height = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4));
		var tiles = new int[width, height];
		Buffer.BlockCopy(bytes, 8, tiles, 0, width * height * sizeof(int));
		return tiles;
	}

	public override void Write(TomlWriter writer, int[,] value)
	{
		var width = value.GetLength(0);
		var height = value.GetLength(1);
		var bytes = new byte[8 + width * height * sizeof(int)];
		BinaryPrimitives.WriteInt32LittleEndian(bytes, width);
		BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), height);
		Buffer.BlockCopy(value, 0, bytes, 8, width * height * sizeof(int));
		writer.WriteStringValue(Convert.ToBase64String(Compress(bytes)));
	}

	private static byte[] Compress(byte[] data)
	{
		using var output = new MemoryStream();
		using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, true))
			deflate.Write(data, 0, data.Length);
		return output.ToArray();
	}

	private static byte[] Decompress(byte[] data)
	{
		using var input = new MemoryStream(data);
		using var deflate = new DeflateStream(input, CompressionMode.Decompress);
		using var output = new MemoryStream();
		deflate.CopyTo(output);
		return output.ToArray();
	}
}
