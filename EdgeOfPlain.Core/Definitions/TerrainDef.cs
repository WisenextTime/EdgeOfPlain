using EdgeOfPlain.Core.Definitions.Worlds;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

[ContentTypeName("terrain")]
[method: TomlConstructor]
public class TerrainDef(string id) : ContentDef(id)
{

	public PassageMask Passage { get; set; } = PassageMask.None;
	public int MoveCost { get; set; } = 1;
	public bool Allows(PassageMask required) => (Passage & required) != PassageMask.None;
}