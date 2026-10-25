using System;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;
using Xunit;

namespace EdgeOfPlain.Tests;

[ContentTypeName("unit-probe")]
internal sealed class UnitProbeDef(string id) : ContentDef(id);

internal sealed class BareProbeDef(string id) : ContentDef(id);

internal abstract class AbstractProbeDef(string id) : ContentDef(id);

internal sealed class GenericProbeDef<T>(string id) : ContentDef(id);

public sealed class ContentStoreTests : IDisposable
{
	public ContentStoreTests() => ContentStore.Reset();

	public void Dispose() => ContentStore.Reset();

	[Fact]
	public void AddAndGet_ReturnsRegisteredDefinition()
	{
		var terrain = new TerrainDef("store-ground") { SourceMod = TestMods.Base };

		ContentStore.Add(terrain);

		Assert.Same(terrain, ContentStore.Get("store-ground"));
		Assert.Same(terrain, ContentStore.Get<TerrainDef>("store-ground"));
		Assert.Null(ContentStore.Get<TilesetDef>("store-ground"));
		Assert.Equal(1, ContentStore.Count);
	}

	[Fact]
	public void Get_UnknownIdReturnsNullAndNullIdThrows()
	{
		Assert.Null(ContentStore.Get("nothing"));
		Assert.Throws<ArgumentNullException>(() => ContentStore.Get(null!));
	}

	[Fact]
	public void All_FiltersByRequestedType()
	{
		ContentStore.Add(new TerrainDef("store-a") { SourceMod = TestMods.Base });
		ContentStore.Add(new TilesetDef("store-b") { SourceMod = TestMods.Base });
		ContentStore.Add(new TerrainDef("store-c") { SourceMod = TestMods.Base });

		Assert.Equal(
			new[] { "store-a", "store-c" },
			ContentStore.All<TerrainDef>().Select(def => def.Id).OrderBy(id => id).ToArray());
		Assert.Single(ContentStore.All<TilesetDef>());
		Assert.Empty(ContentStore.All<TileRendererDef>());
	}

	[Fact]
	public void Add_DuplicateId_Throws()
	{
		ContentStore.Add(new TerrainDef("store-dup") { SourceMod = TestMods.Base });

		Assert.Throws<InvalidOperationException>(() => ContentStore.Add(new TerrainDef("store-dup") { SourceMod = TestMods.Base }));
	}

	[Fact]
	public void Remove_DeletesDefinitionAndThrowsWhenMissing()
	{
		ContentStore.Add(new TerrainDef("store-remove") { SourceMod = TestMods.Base });

		ContentStore.Remove(new TerrainDef("store-remove") { SourceMod = TestMods.Base });

		Assert.Null(ContentStore.Get("store-remove"));
		Assert.False(ContentStore.TryRemove(new TerrainDef("store-remove") { SourceMod = TestMods.Base }));
		Assert.Throws<InvalidOperationException>(() => ContentStore.Remove(new TerrainDef("store-remove") { SourceMod = TestMods.Base }));
	}

	[Fact]
	public void TryRemove_ReportsWhetherDefinitionExisted()
	{
		ContentStore.Add(new TerrainDef("store-try") { SourceMod = TestMods.Base });

		Assert.True(ContentStore.TryRemove(new TerrainDef("store-try") { SourceMod = TestMods.Base }));
		Assert.False(ContentStore.TryRemove(new TerrainDef("store-try") { SourceMod = TestMods.Base }));
		Assert.Equal(0, ContentStore.Count);
	}

	[Fact]
	public void Seal_FreezesTheStore()
	{
		ContentStore.Add(new TerrainDef("store-sealed") { SourceMod = TestMods.Base });
		Assert.False(ContentStore.IsSealed);

		ContentStore.Seal();

		Assert.True(ContentStore.IsSealed);
		Assert.Equal(1, ContentStore.Count);
		Assert.NotNull(ContentStore.Get("store-sealed"));
		Assert.Throws<InvalidOperationException>(() => ContentStore.Add(new TerrainDef("store-late") { SourceMod = TestMods.Base }));
		Assert.Throws<InvalidOperationException>(() => ContentStore.Remove(new TerrainDef("store-sealed") { SourceMod = TestMods.Base }));
		Assert.Throws<InvalidOperationException>(() => ContentStore.TryRemove(new TerrainDef("store-sealed") { SourceMod = TestMods.Base }));
	}

	[Fact]
	public void Reset_ClearsDefinitionsAndSeal()
	{
		ContentStore.Add(new TerrainDef("store-reset") { SourceMod = TestMods.Base });
		ContentStore.Seal();

		ContentStore.Reset();

		Assert.False(ContentStore.IsSealed);
		Assert.Equal(0, ContentStore.Count);
		Assert.Null(ContentStore.Get("store-reset"));
		Assert.Empty(ContentStore.All<ContentDef>());
	}
}

public sealed class ContentTypeRegistryBehaviorTests : IDisposable
{
	public ContentTypeRegistryBehaviorTests() => ContentTypeRegistry.Reset();

	public void Dispose() => ContentTypeRegistry.Reset();

	[Fact]
	public void RegisterType_AddsTypeToSnapshots()
	{
		var version = ContentTypeRegistry.Version;

		ContentTypeRegistry.RegisterType(typeof(UnitProbeDef));

		Assert.Contains(typeof(UnitProbeDef), (IEnumerable<Type>)ContentTypeRegistry.Types);
		Assert.Equal(typeof(UnitProbeDef), ContentTypeRegistry.ByName["unit-probe"]);
		Assert.Equal("unit-probe", ContentTypeRegistry.ByType[typeof(UnitProbeDef)]);
		Assert.True(ContentTypeRegistry.Version > version);
		Assert.Contains(
			ContentTypeRegistry.BuildDerivedTypes(),
			mapping => mapping.DerivedType == typeof(UnitProbeDef) && mapping.Discriminator == "unit-probe");
	}

	[Fact]
	public void RegisterType_RejectsInvalidTypes()
	{
		Assert.Throws<ArgumentNullException>(() => ContentTypeRegistry.RegisterType(null!));
		Assert.Throws<ArgumentException>(() => ContentTypeRegistry.RegisterType(typeof(ContentDef)));
		Assert.Throws<ArgumentException>(() => ContentTypeRegistry.RegisterType(typeof(string)));
		Assert.Throws<ArgumentException>(() => ContentTypeRegistry.RegisterType(typeof(AbstractProbeDef)));
		Assert.Throws<ArgumentException>(() => ContentTypeRegistry.RegisterType(typeof(GenericProbeDef<>)));
	}

	[Fact]
	public void TryUnRegisterType_RemovesRegistration()
	{
		ContentTypeRegistry.RegisterType(typeof(UnitProbeDef));

		Assert.True(ContentTypeRegistry.TryUnRegisterType(typeof(UnitProbeDef)));
		Assert.False(ContentTypeRegistry.TryUnRegisterType(typeof(UnitProbeDef)));
		Assert.DoesNotContain(typeof(UnitProbeDef), (IEnumerable<Type>)ContentTypeRegistry.Types);
		Assert.False(ContentTypeRegistry.ByName.ContainsKey("unit-probe"));
		Assert.False(ContentTypeRegistry.ByType.ContainsKey(typeof(UnitProbeDef)));
	}

	[Fact]
	public void UnRegisterType_ThrowsWhenUnknown()
	{
		Assert.Throws<InvalidOperationException>(() => ContentTypeRegistry.UnRegisterType(typeof(UnitProbeDef)));
	}

	[Fact]
	public void Discriminator_UsesAttributeNameOrTypeName()
	{
		Assert.Equal("unit-probe", ContentTypeRegistry.Discriminator(typeof(UnitProbeDef)));
		Assert.Equal("unit-probe", ContentTypeRegistry.ContentTypeName(typeof(UnitProbeDef)));
		Assert.Equal("BareProbeDef", ContentTypeRegistry.Discriminator(typeof(BareProbeDef)));
		Assert.Equal("BareProbeDef", ContentTypeRegistry.ContentTypeName(typeof(BareProbeDef)));

		ContentTypeRegistry.RegisterType(typeof(BareProbeDef));

		Assert.Equal("BareProbeDef", ContentTypeRegistry.Discriminator(typeof(BareProbeDef)));
	}

	[Fact]
	public void Reset_ClearsSnapshotsAndInvalidatesVersion()
	{
		ContentTypeRegistry.RegisterType(typeof(UnitProbeDef));
		var version = ContentTypeRegistry.Version;

		ContentTypeRegistry.Reset();

		Assert.Empty(ContentTypeRegistry.Types);
		Assert.Empty(ContentTypeRegistry.ByName);
		Assert.Empty(ContentTypeRegistry.ByType);
		Assert.True(ContentTypeRegistry.Version > version);
	}
}
