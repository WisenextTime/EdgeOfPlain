using System;
using Arch.Core;

namespace EdgeOfPlain.Core.Game;

public readonly struct EntityHandle : IEquatable<EntityHandle>, IComparable<EntityHandle>
{
	public readonly Entity Value;

	internal EntityHandle(Entity value) => Value = value;

	public int Id => Value.Id;

	public bool Equals(EntityHandle other) =>
		Value.Id == other.Value.Id && Value.Version == other.Value.Version && Value.WorldId == other.Value.WorldId;

	public override bool Equals(object? obj) => obj is EntityHandle other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(Value.WorldId, Value.Id, Value.Version);

	public int CompareTo(EntityHandle other)
	{
		var c = Value.WorldId.CompareTo(other.Value.WorldId);
		if (c != 0) return c;
		c = Value.Id.CompareTo(other.Value.Id);
		return c != 0 ? c : Value.Version.CompareTo(other.Value.Version);
	}

	public static bool operator ==(EntityHandle left, EntityHandle right) => left.Equals(right);

	public static bool operator !=(EntityHandle left, EntityHandle right) => !left.Equals(right);

	public override string ToString() => $"#{Value.Id}v{Value.Version}";
}