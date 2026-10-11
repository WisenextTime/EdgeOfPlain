using System;
using System.Collections.Generic;
using System.Text.Json;
using EdgeOfPlain.Core.Framework.Mods;
using Tomlyn;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

[TomlPolymorphic]
[method: TomlConstructor]
public abstract class ContentDef(string id) : IComparable<ContentDef>
{
	public string Id { get; } = id;

	public string FullId => ToString();

	[TomlIgnore]
	public ModInfo SourceMod { get; internal set; }

	public int CompareTo(ContentDef? other) => string.Compare(FullId, other?.FullId, StringComparison.Ordinal);

	public override string ToString() => $"{SourceMod.Id}.{ContentTypeRegistry.ByType[GetType()]}({Id})";
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class ContentTypeNameAttribute(string name) : Attribute
{ 
	public string Name { get; } = name;
}

public static class ContentSerializer
{
	private static TomlSerializerOptions _options = CreateOptions();

	public static void ResetOptions() => _options = CreateOptions();

	public static TDef? Deserialize<TDef>(string toml) where TDef : ContentDef
		=> TomlSerializer.Deserialize<TDef>(toml, _options);

	public static string Serialize(ContentDef def) => TomlSerializer.Serialize(def, _options);

	private static TomlSerializerOptions CreateOptions()
	{
		var mappings = new Dictionary<Type, IReadOnlyList<TomlDerivedType>>
		{
			[typeof(ContentDef)] = ContentTypeRegistry.BuildDerivedTypes(),
		};

		return new TomlSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PolymorphismOptions = new TomlPolymorphismOptions
			{
				TypeDiscriminatorPropertyName = "type",
				DerivedTypeMappings = mappings,
			},
		};
	}
}