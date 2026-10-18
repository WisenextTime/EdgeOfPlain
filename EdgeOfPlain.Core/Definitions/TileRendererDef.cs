using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

[ContentTypeName("tileRenderer")]
public class TileRendererDef(string id) : ContentDef(id)
{
	 [TomlRequired]public string ShaderPath { get; set; } = "";
}