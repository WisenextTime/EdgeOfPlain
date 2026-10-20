using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EdgeOfPlain.Core.Definitions;
using Tomlyn;

namespace EdgeOfPlain.Core.Framework.Mods;

public static class ContentTypeRegistry
{
	private static readonly List<Type> _types = [];
	private static readonly Dictionary<string, Type> _byName = new(StringComparer.Ordinal);
	private static readonly Dictionary<Type, string> _byType = [];

	private static FrozenSet<Type>? _typesSnapshot;
	private static FrozenDictionary<string, Type>? _byNameSnapshot;
	private static FrozenDictionary<Type, string>? _byTypeSnapshot;
	private static int _version;

	public static FrozenSet<Type> Types => _typesSnapshot ??= _types.ToFrozenSet();

	public static FrozenDictionary<string, Type> ByName => _byNameSnapshot ??= _byName.ToFrozenDictionary(StringComparer.Ordinal);

	public static FrozenDictionary<Type, string> ByType => _byTypeSnapshot ??= _byType.ToFrozenDictionary();

	public static int Version => Volatile.Read(ref _version);

	public static void RegisterType(Type t)
	{
		ArgumentNullException.ThrowIfNull(t);

		if (t == typeof(ContentDef) || !typeof(ContentDef).IsAssignableFrom(t))
			throw new ArgumentException($"'{t}' is not a content definition type", nameof(t));
		if (t.IsAbstract || t.IsGenericTypeDefinition)
			throw new ArgumentException($"'{t}' must be a concrete type", nameof(t));

		var typeName = ContentTypeName(t);

		if (_byName.TryGetValue(typeName, out var nameOwner))
			throw new InvalidOperationException($"Type name '{typeName}' is already registered to '{nameOwner.Name}'");
		if (_byType.TryGetValue(t, out var typeNameOwner))
			throw new InvalidOperationException($"Type '{t.Name}' is already registered as '{typeNameOwner}'");

		_byType.Add(t, typeName);
		_byName.Add(typeName, t);
		_types.Add(t);
		Invalidate();
	}

	internal static bool TryUnRegisterType(Type t)
	{
		if (!_byType.Remove(t, out var name)) return false;

		_byName.Remove(name);
		_types.Remove(t);
		Invalidate();
		return true;
	}

	internal static void UnRegisterType(Type t)
	{
		if (!TryUnRegisterType(t)) throw new InvalidOperationException($"Type '{t.Name}' is not registered");
	}

	internal static void Reset()
	{
		_types.Clear();
		_byName.Clear();
		_byType.Clear();
		Invalidate();
	}

	public static IReadOnlyList<TomlDerivedType> BuildDerivedTypes()
		=> ByType.Select(pair => new TomlDerivedType(pair.Key, pair.Value)).ToArray();

	internal static string Discriminator(Type t)
		=> ByType.TryGetValue(t, out var name) ? name : ContentTypeName(t);

	internal static string ContentTypeName(Type t)
		=> t.GetCustomAttributes(typeof(ContentTypeNameAttribute), true) is [ContentTypeNameAttribute name, ..]
			? name.Name
			: t.Name;

	private static void Invalidate()
	{
		_typesSnapshot = null;
		_byNameSnapshot = null;
		_byTypeSnapshot = null;
		Interlocked.Increment(ref _version);
	}
}