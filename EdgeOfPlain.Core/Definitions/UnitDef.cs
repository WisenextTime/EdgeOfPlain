namespace EdgeOfPlain.Core.Definitions;

[ContentTypeName("unit")]
public class UnitDef(string id) : EntityDef(id)
{
	public float MaxVelocity { get; set; } = 120f;
	public float Acceleration { get; set; } = 70f;

	public float RotationalVelocity { get; set; } = 5f;
	public bool AlignBeforeTurn { get; set; }
	
}