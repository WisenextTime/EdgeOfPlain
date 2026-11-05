using System;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Game;

public struct UnitId(int value) : INetSerializable, IEquatable<UnitId>, IComparable<UnitId>
{
	public const int None = 0;

	public static UnitId Invalid => default;

	public int Value { get; private set; } = value;

	public bool IsValid => Value > None;

	public int CompareTo(UnitId other) => Value.CompareTo(other.Value);

	public bool Equals(UnitId other) => Value == other.Value;

	public override bool Equals(object? obj) => obj is UnitId other && Equals(other);

	public override int GetHashCode() => Value;

	public static bool operator ==(UnitId left, UnitId right) => left.Value == right.Value;

	public static bool operator !=(UnitId left, UnitId right) => left.Value != right.Value;

	public void Serialize(NetDataWriter writer) => writer.Put(Value);

	public void Deserialize(NetDataReader reader) => Value = reader.GetInt();

	public override string ToString() => IsValid ? $"u{Value}" : "<none>";
}