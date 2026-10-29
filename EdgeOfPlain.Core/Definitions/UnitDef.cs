using EdgeOfPlain.Core.Definitions.Worlds;

namespace EdgeOfPlain.Core.Definitions;

[ContentTypeName("unit")]
public class UnitDef(string id) : EntityDef(id)
{
	public float MaxVelocity { get; set; } = 60f;
	public float Acceleration { get; set; } = 1f;

	public float MaxRotationalVelocity { get; set; } = 1f;
	public float RotationalAcceleration { get; set; } = 1f;
	public bool AlignBeforeTurn { get; set; } = true;
	
}