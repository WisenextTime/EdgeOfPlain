using System;
using EdgeOfPlain.Core.Definitions;

namespace EdgeOfPlain.Core.Content.Components;

public struct CollisionComponent
{
	public bool EnableCollision;
	public int Radius;

	public CollisionComponent() : this(true, EntityDef.DefaultCollisionRadius) { }

	public CollisionComponent(bool enableCollision, int radius)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(radius);
		EnableCollision = enableCollision;
		Radius = radius;
	}

	public static CollisionComponent CreateComponent(EntityDef def) => new(def.EnableCollision, def.Radius);
}
