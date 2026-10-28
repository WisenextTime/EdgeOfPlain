using System.Numerics;
using EdgeOfPlain.Core.Definitions;

namespace EdgeOfPlain.Core.Content.Components;

public struct TransformComponent
{
	public Vector2 Position;
	public float Rotation;

	public static TransformComponent CreateComponent(EntityDef def)
	{
		return new TransformComponent
		{
			Position = Vector2.Zero,
			Rotation = 0f,
		};
	}
}
