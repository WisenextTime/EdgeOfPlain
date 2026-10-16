using System.Collections.Generic;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

[ContentTypeName("tileset")]
public class TilesetDef(string id) : ContentDef(id)
{
	[TomlRequired]public int Width { get; set; }
	[TomlRequired]public int Height { get; set; }
	public int TileWidth { get; set; } = 32;
	public int TileHeight { get; set; } = 32;

	public int LeftMargin { get; set; } = 0;
	public int TopMargin { get; set; } = 0;
	public int RightMargin { get; set; } = 0;
	public int BottomMargin { get; set; } = 0;

	public int HorizontalSpacing { get; set; } = 0;
	public int VerticalSpacing { get; set; } = 0;

	public Dictionary<string, TileData> TileData { get; set; } = [];
}

public struct TileData
{
	public string Terrain
	{
		get => field ?? "ground";
		set;
	}

	public string Renderer
	{
		get => field?? "default";
		set;
	}

	public int[]? Animation { get; set; }
	public int[]? Random { get; set; }
}