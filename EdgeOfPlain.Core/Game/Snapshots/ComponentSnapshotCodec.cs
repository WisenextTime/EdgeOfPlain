using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EdgeOfPlain.Core.Game.Snapshots;

internal abstract class ComponentSnapshotCodec
{
	private static readonly ConcurrentDictionary<Type, ComponentSnapshotCodec> _codecs = new();

	public static string Signature(Type type) => $"{type.Assembly.GetName().Name}:{type.FullName}";

	public static ComponentSnapshotCodec For(Type type) => _codecs.GetOrAdd(type, static value =>
	{
		if (HasProcessReferences(value, []))
			throw new NotSupportedException($"Component '{value}' contains process-local references; use UnitId for entity links.");
		return (ComponentSnapshotCodec)Activator.CreateInstance(typeof(Codec<>).MakeGenericType(value))!;
	});

	private static bool HasProcessReferences(Type type, HashSet<Type> visited)
	{
		if (type == typeof(Arch.Core.Entity) || type == typeof(EntityHandle) || type == typeof(IntPtr) ||
		    type == typeof(UIntPtr) || type.IsPointer || !type.IsValueType) return true;
		if (type.IsPrimitive || type.IsEnum || !visited.Add(type)) return false;
		foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			if (HasProcessReferences(field.FieldType, visited)) return true;
		return false;
	}

	public abstract byte[] Encode(object value);
	public abstract object Decode(byte[] bytes);

	private sealed class Codec<T> : ComponentSnapshotCodec where T : struct
	{
		public Codec()
		{
			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
				throw new NotSupportedException($"Snapshot component '{typeof(T)}' contains managed references.");
		}

		public override byte[] Encode(object value)
		{
			var bytes = new byte[Unsafe.SizeOf<T>()];
			var component = (T)value;
			MemoryMarshal.Write(bytes, in component);
			return bytes;
		}

		public override object Decode(byte[] bytes)
		{
			if (bytes.Length != Unsafe.SizeOf<T>()) throw new InvalidDataException("Component layout does not match.");
			return MemoryMarshal.Read<T>(bytes);
		}
	}
}
