using System;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.Definitions;
using Tomlyn;

namespace EdgeOfPlain.Core.Framework.Mods;

public static class ContentTypeRegistry
{
	public static List<Type> Types { get; } = [];
	public static Dictionary<string, Type> ByName { get; } = new();
	public static Dictionary<Type, string> ByType { get; } = new();
	

	public static void RegisterType(Type t)
	{
		var nameAttributes = t.GetCustomAttributes(typeof(ContentTypeNameAttribute), true) as ContentTypeNameAttribute[];
		var typeName = nameAttributes?.Length > 0 ? nameAttributes[0].Name : t.Name;
		if(Types.Contains(t)) throw new InvalidOperationException($"Type {typeName} already registered");
		Types.Add(t);
		ByName.Add(typeName, t);
		ByType.Add(t, typeName);
	}

	public static IReadOnlyList<TomlDerivedType> BuildDerivedTypes()
	{
		var list = new List<TomlDerivedType>();
		list.AddRange(ByType.Select(kvp => new TomlDerivedType(kvp.Key, kvp.Value)));
		return list;
	}
}