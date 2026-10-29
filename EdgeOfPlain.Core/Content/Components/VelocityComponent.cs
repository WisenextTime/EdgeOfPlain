using EdgeOfPlain.Core.Definitions;

namespace EdgeOfPlain.Core.Content.Components;

public struct VelocityComponent
{
	public float MaxVelocity;
	public float Acceleration;
	public float Velocity;

	public float MaxRotationalVelocity;
	public float RotationalAcceleration;
	public float RotationalVelocity;

	public bool AlignBeforeTurn;

	public static VelocityComponent CreateComponent(UnitDef def)
	{
		return new VelocityComponent
		{
			MaxVelocity = def.MaxVelocity,
			Acceleration = def.Acceleration,
			MaxRotationalVelocity = def.MaxRotationalVelocity,
			RotationalAcceleration = def.RotationalAcceleration,
			AlignBeforeTurn = def.AlignBeforeTurn,
		};
	}
}
