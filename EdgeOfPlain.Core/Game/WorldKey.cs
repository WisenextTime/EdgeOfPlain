using System;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Game;

public struct WorldKey(int value) : INetSerializable, IEquatable<WorldKey>, IComparable<WorldKey>
{
	public const int None = 0;

	public static WorldKey Invalid => default;

	public static WorldKey Primary => new(1);

	public int Value { get; private set; } = value;

	public bool IsValid => Value > None;

	public int CompareTo(WorldKey other) => Value.CompareTo(other.Value);

	public bool Equals(WorldKey other) => Value == other.Value;

	public override bool Equals(object? obj) => obj is WorldKey other && Equals(other);

	public override int GetHashCode() => Value;

	public static bool operator ==(WorldKey left, WorldKey right) => left.Value == right.Value;

	public static bool operator !=(WorldKey left, WorldKey right) => left.Value != right.Value;

	public void Serialize(NetDataWriter writer) => writer.Put(Value);

	public void Deserialize(NetDataReader reader) => Value = reader.GetInt();

	public override string ToString() => IsValid ? $"w{Value}" : "<none>";
}