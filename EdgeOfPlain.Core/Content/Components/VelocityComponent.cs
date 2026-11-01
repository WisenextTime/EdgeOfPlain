using EdgeOfPlain.Core.Definitions;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Content.Components;

public struct VelocityComponent
{
	public Fixed64 MaxVelocity;
	public Fixed64 Acceleration;
	public Fixed64 Velocity;

	public Fixed64 RotationalVelocity;

	public bool AlignBeforeTurn;

	public static VelocityComponent CreateComponent(UnitDef def)
	{
		return new VelocityComponent
		{
			MaxVelocity = def.MaxVelocity,
			Acceleration = def.Acceleration,
			RotationalVelocity = def.RotationalVelocity,
			AlignBeforeTurn = def.AlignBeforeTurn,
		};
	}
}
