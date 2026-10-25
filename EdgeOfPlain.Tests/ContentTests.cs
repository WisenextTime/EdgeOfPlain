using System;
using System.Collections.Generic;
using System.Reflection;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Framework.Mods;
using Xunit;

namespace EdgeOfPlain.Tests;

[ContentTypeName("probe")]
public sealed class ProbeDef(string id) : ContentDef(id)
{
	public int Value { get; set; }
}

[ContentTypeName("late")]
public sealed class LateProbeDef(string id) : ContentDef(id)
{
	public int Value { get; set; }
}

[ContentTypeName("dup")]
public sealed class DuplicateProbeDef(string id) : ContentDef(id);

[ContentTypeName("shared")]
public sealed class SharedNameProbeDef(string id) : ContentDef(id);

[ContentTypeName("shared")]
public sealed class SharedNameProbeDef2(string id) : ContentDef(id);

[ContentTypeName("snapshot")]
public sealed class SnapshotProbeDef(string id) : ContentDef(id);

internal static class RegistryProbes
{
	public static void Ensure(Type type)
	{
		if (!ContentTypeRegistry.ByType.ContainsKey(type)) ContentTypeRegistry.RegisterType(type);
	}
}

public sealed class TerrainTests
{
	[Fact]
	public void Allows_MatchesAnyRequestedFlag()
	{
		var ground = new TerrainDef("ground") { Passage = PassageMask.Ground };

		Assert.True(ground.Allows(PassageMask.Ground));
		Assert.True(ground.Allows(PassageMask.Air | PassageMask.Ground));
		Assert.False(ground.Allows(PassageMask.Air));
		Assert.False(ground.Allows(PassageMask.Water | PassageMask.Cliff));
	}

	[Fact]
	public void Allows_IsFalseForEmptyRequirement()
	{
		var airAndGround = new TerrainDef("x")
		{
			Passage = PassageMask.Air | PassageMask.Ground,
		};

		Assert.True(airAndGround.Allows(PassageMask.Air));
		Assert.True(airAndGround.Allows(PassageMask.Air | PassageMask.Water));
		Assert.False(airAndGround.Allows(PassageMask.None));
		Assert.False(new TerrainDef("bare").Allows(PassageMask.Air));
	}

	[Fact]
	public void DefaultsAreMoveCostOneAndNoPassage()
	{
		var terrain = new TerrainDef("bare");

		Assert.Equal(1, terrain.MoveCost);
		Assert.Equal(PassageMask.None, terrain.Passage);
	}
}

public sealed class ContentTypeRegistryTests
{
	[Fact]
	public void RegisterType_RejectsDuplicateType()
	{
		var type = typeof(DuplicateProbeDef);
		RegistryProbes.Ensure(type);

		Assert.Throws<InvalidOperationException>(() => ContentTypeRegistry.RegisterType(type));
	}

	[Fact]
	public void RegisterType_RejectsDuplicateDiscriminator()
	{
		RegistryProbes.Ensure(typeof(SharedNameProbeDef));

		Assert.Throws<InvalidOperationException>(() => ContentTypeRegistry.RegisterType(typeof(SharedNameProbeDef2)));
	}

	[Fact]
	public void RegisteredTypesAreVisibleInSnapshots()
	{
		RegistryProbes.Ensure(typeof(SnapshotProbeDef));

		Assert.Contains(typeof(SnapshotProbeDef), (IEnumerable<Type>)ContentTypeRegistry.Types);
		Assert.Equal(typeof(SnapshotProbeDef), ContentTypeRegistry.ByName["snapshot"]);
		Assert.Equal("snapshot", ContentTypeRegistry.ByType[typeof(SnapshotProbeDef)]);
		Assert.NotEmpty(ContentTypeRegistry.BuildDerivedTypes());
	}

	[Fact]
	public void ContentTypeNameAttributeOverridesTypeName()
	{
		Assert.Equal("probe", typeof(ProbeDef).GetCustomAttribute<ContentTypeNameAttribute>()!.Name);
		Assert.Null(typeof(NoAttributeProbeDef).GetCustomAttribute<ContentTypeNameAttribute>());
	}
}

public sealed class NoAttributeProbeDef(string id) : ContentDef(id);

public sealed class ContentDefPathTests
{
	[Fact]
	public void ResolveResourcePathPrefixesPathsWithTheSourceMod()
	{
		var def = new ProbeDef("probe") { SourceMod = new ModInfo("test", new Version(1, 0, 0)) };

		Assert.Equal("assets://test/path/to/resource.res", def.ResolveResourcePath("path/to/resource.res"));
		Assert.Equal("assets://test/path/to/resource.res", def.ResolveResourcePath("/path/to/resource.res"));
		Assert.Equal("assets://test/path/to/resource.res", def.ResolveResourcePath(@"path\to\resource.res"));
	}

	[Fact]
	public void ResolveResourcePathKeepsAbsoluteAssetPaths()
	{
		var def = new ProbeDef("probe") { SourceMod = new ModInfo("test", new Version(1, 0, 0)) };

		Assert.Equal("assets://other/path.res", def.ResolveResourcePath("assets://other/path.res"));
	}

	[Fact]
	public void ResolveResourcePathRejectsEmptyPaths()
	{
		var def = new ProbeDef("probe") { SourceMod = new ModInfo("test", new Version(1, 0, 0)) };

		Assert.Throws<ArgumentException>(() => def.ResolveResourcePath(""));
		Assert.Throws<ArgumentException>(() => def.ResolveResourcePath("   "));
	}
}

public sealed class ContentSerializerTests
{
	[Fact]
	public void TerrainRoundTripsThroughToml()
	{
		RegistryProbes.Ensure(typeof(TerrainDef));
		ContentSerializer.ResetOptions();

		var original = new TerrainDef("mud")
		{
			Passage = PassageMask.Ground,
			MoveCost = 3,
		};

		var toml = ContentSerializer.Serialize(original);
		var restored = ContentSerializer.Deserialize<ContentDef>(toml);

		var terrain = Assert.IsType<TerrainDef>(restored);
		Assert.Equal("mud", terrain.Id);
		Assert.Equal(3, terrain.MoveCost);
		Assert.Equal(PassageMask.Ground, terrain.Passage);
	}

	[Fact]
	public void TilesetTomlCanBeDeserialized()
	{
		RegistryProbes.Ensure(typeof(TilesetDef));
		ContentSerializer.ResetOptions();

		var toml = """
		           type = "tileset"
		           id = "tiles"
		           texture = "assets://test/Tiles#main"
		           width = 8
		           height = 4
		           tileWidth = 16
		           """;

		var restored = ContentSerializer.Deserialize<ContentDef>(toml);

		var tileset = Assert.IsType<TilesetDef>(restored);
		Assert.Equal(8, tileset.Width);
		Assert.Equal(4, tileset.Height);
		Assert.Equal(16, tileset.TileWidth);
	}

	[Fact]
	public void SerializerPicksUpTypesRegisteredLater()
	{
		ContentSerializer.ResetOptions();
		RegistryProbes.Ensure(typeof(LateProbeDef));

		var restored = ContentSerializer.Deserialize<ContentDef>("type = \"late\"\nid = \"x\"\nvalue = 7\n");

		Assert.Equal(7, Assert.IsType<LateProbeDef>(restored).Value);
	}

	[Fact]
	public void ContentDefToStringContainsTypeAndId()
	{
		RegistryProbes.Ensure(typeof(DuplicateProbeDef));

		var text = new DuplicateProbeDef("mud") { SourceMod = new ModInfo("test", new Version(1, 0, 0)) }.ToString();

		Assert.Contains("dup", text);
		Assert.Contains("mud", text);
	}
}