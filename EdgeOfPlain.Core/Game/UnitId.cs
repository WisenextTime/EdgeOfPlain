using System;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Game;

public struct UnitId(int value) : INetSerializable, IEquatable<UnitId>, IComparable<UnitId>
{
	public const int None = 0;

	private int _value = value;

	public static UnitId Invalid => default;

	public int Value => _value;

	public bool IsValid => _value > None;

	public int CompareTo(UnitId other) => _value.CompareTo(other._value);

	public bool Equals(UnitId other) => _value == other._value;

	public override bool Equals(object? obj) => obj is UnitId other && Equals(other);

	public override int GetHashCode() => _value;

	public static bool operator ==(UnitId left, UnitId right) => left._value == right._value;

	public static bool operator !=(UnitId left, UnitId right) => left._value != right._value;

	public void Serialize(NetDataWriter writer) => writer.Put(_value);

	public void Deserialize(NetDataReader reader) => _value = reader.GetInt();

	public override string ToString() => IsValid ? $"u{_value}" : "<none>";
}