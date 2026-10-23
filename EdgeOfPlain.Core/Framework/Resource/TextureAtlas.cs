using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Tomlyn;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Framework.Resource;

public sealed class TextureAtlas
{
	private static readonly TomlSerializerOptions _options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	[TomlRequired]
	[TomlPropertyName("Sprite")]
	public List<SpriteData> Sprites { get; set; } = [];

	public static TextureAtlas Parse(string toml)
		=> TomlSerializer.Deserialize<TextureAtlas>(toml, _options)
		   ?? throw new InvalidDataException("the atlas does not describe any sprite");

	public struct SpriteData
	{
		[TomlRequired] public string Id { get; set; }
		[TomlRequired] public int X { get; set; }
		[TomlRequired] public int Y { get; set; }
		[TomlRequired] public int Width { get; set; }
		[TomlRequired] public int Height { get; set; }
	}
}
