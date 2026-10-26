using System.Numerics;

namespace EdgeOfPlain.Core.Definitions.Components;

public struct TransformComponent
{
	public Vector2 Position;
	public float Rotation;
	
	public static TransformComponent CreateComponent(UnitDef def)
	{
		return new TransformComponent
		{
			Position = Vector2.Zero,
			Rotation = 0f,
		};
	}
}