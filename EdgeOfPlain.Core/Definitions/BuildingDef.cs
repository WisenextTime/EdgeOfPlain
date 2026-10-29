using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

[ContentTypeName("building")]
public class BuildingDef(string id) : EntityDef(id)
{
	public Footprint Construction { get; set; }
	public Footprint Collision { get; set; }
}

public record struct Footprint(int Left, int Up, int Right, int Down);