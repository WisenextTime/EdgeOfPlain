using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using EdgeOfPlain.Core.Framework.Mods;
using Tomlyn;
using Tomlyn.Serialization;

namespace EdgeOfPlain.Core.Definitions;

[TomlPolymorphic]
[method: TomlConstructor]
public abstract class ContentDef(string id) : IComparable<ContentDef>
{
	public string Id { get; } = id;

	[TomlIgnore]
	public string FullId => ToString();

	[TomlIgnore]
	public ModInfo SourceMod { get; internal set; }

	public int CompareTo(ContentDef? other) => string.Compare(FullId, other?.FullId, StringComparison.Ordinal);
	
	public override string ToString() => $"{SourceMod.Id}.{ContentTypeRegistry.Discriminator(GetType())}({Id})";
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class ContentTypeNameAttribute(string name) : Attribute
{
	public string Name { get; } = name;
}

public static class ContentSerializer
{
	private static readonly Lock _gate = new();
	private static TomlSerializerOptions _options = CreateOptions();
	private static int _version = ContentTypeRegistry.Version;

	public static void ResetOptions()
	{
		lock (_gate)
		{
			_options = CreateOptions();
			_version = ContentTypeRegistry.Version;
		}
	}

	public static TDef? Deserialize<TDef>(string toml) where TDef : ContentDef
		=> TomlSerializer.Deserialize<TDef>(toml, CurrentOptions);

	public static string Serialize(ContentDef def) => TomlSerializer.Serialize(def, CurrentOptions);

	private static TomlSerializerOptions CurrentOptions
	{
		get
		{
			var version = ContentTypeRegistry.Version;
			if (version == Volatile.Read(ref _version)) return _options;

			lock (_gate)
			{
				if (version != _version)
				{
					_options = CreateOptions();
					_version = version;
				}

				return _options;
			}
		}
	}

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
