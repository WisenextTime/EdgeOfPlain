using System.Collections.Generic;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Framework.Resource;

public struct TextureAtlas
{
	[TomlRequired] public Dictionary<string, SpriteData> Sprites;
	public struct SpriteData
	{
		[TomlRequired] public string Id { get; set; }
		[TomlRequired] public int X { get; set; }
		[TomlRequired] public int Y { get; set; }
		[TomlRequired] public int Width { get; set; }
		[TomlRequired] public int Height { get; set; }
	}
}