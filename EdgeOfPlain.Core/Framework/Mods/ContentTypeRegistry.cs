using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.Definitions;
using Tomlyn;

namespace EdgeOfPlain.Core.Framework.Mods;

public static class ContentTypeRegistry
{
	private static readonly List<Type> _types = [];
	private static readonly Dictionary<string, Type> _byName = new(StringComparer.Ordinal);
	private static readonly Dictionary<Type, string> _byType = [];

	public static FrozenSet<Type> Types => _types.ToFrozenSet();
	public static FrozenDictionary<string, Type> ByName => _byName.ToFrozenDictionary(StringComparer.Ordinal);
	public static FrozenDictionary<Type, string> ByType => _byType.ToFrozenDictionary();

	public static void RegisterType(Type t)
	{
		var typeName = ContentTypeName(t);

		if (_byName.TryGetValue(typeName, out var value))
			throw new InvalidOperationException($"Type name {typeName} is already registered to {value.Name}");
		if (_byType.TryGetValue(t, out var value1))
			throw new InvalidOperationException($"Type {t.Name} is already registered as {value1}");

		_byType.Add(t, typeName);
		_byName.Add(typeName, t);
		_types.Add(t);
	}

	internal static bool TryUnRegisterType(Type t)
	{
		if (!_byType.Remove(t, out var name)) return false;
		_byName.Remove(name);
		_types.Remove(t);
		return true;
	}

	internal static void UnRegisterType(Type t)
	{
		if (!TryUnRegisterType(t)) throw new InvalidOperationException($"Type {t.Name} is not registered");
	}

	public static IReadOnlyList<TomlDerivedType> BuildDerivedTypes()
	{
		var list = new List<TomlDerivedType>();
		list.AddRange(_byType.Select(kvp => new TomlDerivedType(kvp.Key, kvp.Value)));
		return list;
	}

	internal static string ContentTypeName(Type t)
		=> t.GetCustomAttributes(typeof(ContentTypeNameAttribute), true) is [ContentTypeNameAttribute name, ..]
			? name.Name
			: t.Name;
}
