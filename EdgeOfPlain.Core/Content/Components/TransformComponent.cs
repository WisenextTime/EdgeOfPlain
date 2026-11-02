using EdgeOfPlain.Core.Definitions;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Content.Components;

public struct TransformComponent
{
	public Vector2d Position;
	public Fixed64 Rotation;

	public Vector2d PreviousPosition;
	public Fixed64 PreviousRotation;

	public static TransformComponent CreateComponent(EntityDef def)
	{
		return new TransformComponent
		{
			Position = Vector2d.Zero,
			Rotation = Fixed64.Zero,
		};
	}
}
