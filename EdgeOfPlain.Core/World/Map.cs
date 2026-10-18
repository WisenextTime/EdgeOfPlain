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
	public List<TileSetData> Tilesets { get; set; } = [];
	public int Width => Tiles.GetLength(0);
	public int Height => Tiles.GetLength(1);
}

public struct TileSetData
{
	public TileSetData() { }

	public string TileSet { get; set; } = string.Empty;
	public ushort FirstGid { get; set; }
}

public sealed class TileGridConverter : TomlConverter<ushort[,]>
{
	public override ushort[,] Read(TomlReader reader)
	{
		var source = reader.GetString();
		var decompressed = Decompress(source);

		if (decompressed.Length < 2 * sizeof(int))
			throw new InvalidDataException($"Tile grid payload is truncated: {decompressed.Length} byte");

		var rows = BinaryPrimitives.ReadInt32LittleEndian(decompressed.AsSpan(0));
		var cols = BinaryPrimitives.ReadInt32LittleEndian(decompressed.AsSpan(4));

		if (rows < 0 || cols < 0)
			throw new InvalidDataException($"Tile grid has a negative size ({rows}x{cols})");

		var length = (long)rows * cols * sizeof(ushort);
		if (length + 2 * sizeof(int) > decompressed.Length)
			throw new InvalidDataException(
				$"Tile grid payload is truncated: {rows}x{cols} needs {length} byte but only {decompressed.Length - 2 * sizeof(int)} byte remained");

		var value = new ushort[rows, cols];
		Buffer.BlockCopy(decompressed, 8, value, 0, (int)length);
		return value;
	}

	public override void Write(TomlWriter writer, ushort[,] value)
	{
		var rows = value.GetLength(0);
		var cols = value.GetLength(1);
		var length = (long)rows * cols * sizeof(ushort);

		if (length + 2 * sizeof(int) > int.MaxValue)
			throw new InvalidOperationException($"Tile grid {rows}x{cols} is too large to serialize");

		var raw = new byte[length + 2 * sizeof(int)];
		BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(0), rows);
		BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(4), cols);
		Buffer.BlockCopy(value, 0, raw, 8, (int)length);
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
