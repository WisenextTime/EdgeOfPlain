using System;
using System.Text.Json;
using EdgeOfPlain.Core.Definitions.Worlds;
using Tomlyn;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class MapTests
{
	private static TomlSerializerOptions Options => new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	[Fact]
	public void SizeFollowsTileGrid()
	{
		var map = new Map(5, 3);

		Assert.Equal(5, map.Width);
		Assert.Equal(3, map.Height);
		Assert.Equal(0, map.Tiles[4, 2]);
	}

	[Fact]
	public void RoundTripPreservesEveryTile()
	{
		var map = new Map(4, 3);
		for (var x = 0; x < map.Width; x++)
		for (var y = 0; y < map.Height; y++)
		{
			map.Tiles[x, y] = (ushort)(x * 10 + y + 1);
		}

		var toml = TomlSerializer.Serialize(map, Options);
		var restored = TomlSerializer.Deserialize<Map>(toml, Options);

		Assert.NotNull(restored);
		Assert.Equal(map.Width, restored.Width);
		Assert.Equal(map.Height, restored.Height);

		for (var x = 0; x < map.Width; x++)
		for (var y = 0; y < map.Height; y++)
		{
			Assert.Equal(map.Tiles[x, y], restored.Tiles[x, y]);
		}
	}

	[Fact]
	public void RoundTripPreservesTilesets()
	{
		var map = new Map(2, 2);
		map.Tilesets.Add(new TileSetData { TileSet = "TestTile", FirstGid = 7 });

		var toml = TomlSerializer.Serialize(map, Options);
		var restored = TomlSerializer.Deserialize<Map>(toml, Options);

		Assert.NotNull(restored);
		var tileset = Assert.Single(restored.Tilesets);
		Assert.Equal("TestTile", tileset.TileSet);
		Assert.Equal(7, tileset.FirstGid);
	}

	[Fact]
	public void ConverterRejectsTruncatedPayload()
	{
		var map = new Map(2, 2);
		var toml = TomlSerializer.Serialize(map, Options);

		var tampered = toml.Replace("tiles = ", "tiles = \"AAAA\"").Split('\n');
		var broken = string.Join('\n', tampered);

		Assert.ThrowsAny<Exception>(() => TomlSerializer.Deserialize<Map>(broken, Options));
	}
}