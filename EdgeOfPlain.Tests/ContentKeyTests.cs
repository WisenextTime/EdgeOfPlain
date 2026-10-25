using System;
using System.Linq;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class ContentKeyTests
{
	[Theory]
	[InlineData("base:terrain/ground", "base", "terrain", "ground")]
	[InlineData("terrain/ground", "", "terrain", "ground")]
	[InlineData("my-mod:terrain/ground", "my-mod", "terrain", "ground")]
	[InlineData("base:terrain/a/b", "base", "terrain", "a/b")]
	public void TryParse_SplitsModTypeAndLocalId(string text, string modId, string type, string localId)
	{
		Assert.True(ContentKey.TryParse(text, out var parsed));
		Assert.Equal(new ContentKey(modId, type, localId), parsed!.Value);
		if (modId.Length != 0) Assert.Equal(text, parsed.Value.ToString());
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("terrain")]
	[InlineData("terrain/")]
	[InlineData("/Ground")]
	[InlineData(":terrain/Ground")]
	[InlineData("base:/Ground")]
	[InlineData("base:terrain/")]
	public void TryParse_RejectsMalformedText(string text)
	{
		Assert.False(ContentKey.TryParse(text, out _));
		Assert.Throws<FormatException>(() => ContentKey.Parse(text));
	}

	[Fact]
	public void Parse_ReturnsKeyAndRejectsNull()
	{
		Assert.Equal(new ContentKey("base", "terrain", "ground"), ContentKey.Parse("base:terrain/ground"));
		Assert.Throws<ArgumentNullException>(() => ContentKey.Parse(null!));
	}

	[Fact]
	public void CompareTo_OrdersByTextForm()
	{
		var keys = new[]
		{
			new ContentKey("beta", "terrain", "a"),
			new ContentKey("alpha", "terrain", "b"),
			new ContentKey("alpha", "terrain", "a"),
		};

		Assert.Equal(
			["alpha:terrain/a", "alpha:terrain/b", "beta:terrain/a"],
			keys.Order().Select(key => key.ToString()));
	}

	[Fact]
	public void ToString_AlwaysWritesTheModPrefix()
	{
		Assert.Equal("base:terrain/ground", new ContentKey("base", "terrain", "ground").ToString());
		Assert.Equal(":terrain/ground", new ContentKey("", "terrain", "ground").ToString());
	}
}

public sealed class ContentStoreResolutionTests : IDisposable
{
	private static readonly ModInfo _alpha = new("alpha", new Version(1, 0, 0));
	private static readonly ModInfo _beta = new("beta", new Version(1, 0, 0));

	public ContentStoreResolutionTests() => ContentStore.Reset();

	public void Dispose() => ContentStore.Reset();

	private static void Arrange()
	{
		ContentStore.Add(new TerrainDef("Ore") { SourceMod = _alpha, MoveCost = 1 });
		ContentStore.Add(new TerrainDef("Ore") { SourceMod = _beta, MoveCost = 2 });
		ContentStore.Add(new TilesetDef("Ore") { SourceMod = _beta });
	}

	[Fact]
	public void BareIdIsAmbiguousAcrossMods()
	{
		Arrange();

		var exception = Assert.Throws<ContentResolutionException>(() => ContentStore.Resolve<TerrainDef>("Ore"));

		Assert.Contains("alpha:terrain/Ore", exception.Message);
		Assert.Contains("beta:terrain/Ore", exception.Message);
		Assert.Throws<ContentResolutionException>(() => ContentStore.Get<TerrainDef>("Ore"));
	}

	[Fact]
	public void BareIdResolvesInsideTheReferrersMod()
	{
		Arrange();

		Assert.Equal(2, ContentStore.Resolve<TerrainDef>("Ore", Referrer(_beta)).MoveCost);
		Assert.Equal(1, ContentStore.Get<TerrainDef>("Ore", Referrer(_alpha))!.MoveCost);
	}

	[Fact]
	public void BareIdDoesNotEscapeTheReferrersMod()
	{
		Arrange();

		var exception = Assert.Throws<ContentResolutionException>(
			() => ContentStore.Resolve<TerrainDef>("Ore", Referrer(new ModInfo("nobody", new Version(1, 0, 0)))));

		Assert.Contains("not registered", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void QualifiedKeyBypassesAmbiguity()
	{
		Arrange();

		var alpha = ContentStore.Get<TerrainDef>(new ContentKey("alpha", "terrain", "Ore"));

		Assert.Equal(1, alpha!.MoveCost);
		Assert.Equal(2, ContentStore.Get<TerrainDef>("beta:terrain/Ore")!.MoveCost);
		Assert.Equal("beta", ContentStore.Get<TerrainDef>("beta:terrain/Ore")!.SourceMod.Id);
	}

	[Fact]
	public void TypeQualifiedReferenceNarrowsByType()
	{
		Arrange();

		Assert.Equal(2, ContentStore.Resolve<TerrainDef>("terrain/Ore", Referrer(_beta)).MoveCost);
		Assert.Equal("beta", ContentStore.Get<TilesetDef>("tileset/Ore")!.SourceMod.Id);
		Assert.Equal("Ore", ContentStore.Get<TilesetDef>("tileset/Ore")!.Id);
	}

	[Fact]
	public void MissingAndWrongTypeReferencesReturnNull()
	{
		Arrange();

		Assert.Null(ContentStore.Get<TerrainDef>("Nothing"));
		Assert.Null(ContentStore.Get<TerrainDef>("tileset/Ore"));
		Assert.Null(ContentStore.Get<TerrainDef>("nobody:terrain/Ore"));
		Assert.Throws<ContentResolutionException>(() => ContentStore.Resolve<TerrainDef>("Nothing"));
		Assert.Throws<ContentResolutionException>(() => ContentStore.Resolve<TerrainDef>("nobody:terrain/Ore"));
	}

	[Fact]
	public void TryResolveReportsFailureWithoutThrowing()
	{
		Arrange();

		Assert.True(ContentStore.TryResolve("alpha:terrain/Ore", out TerrainDef? resolved));
		Assert.Equal(1, resolved!.MoveCost);

		Assert.False(ContentStore.TryResolve("nobody:terrain/Ore", out TerrainDef? missing));
		Assert.Null(missing);
	}

	[Fact]
	public void KeysAndAllExposeEveryDefinition()
	{
		Arrange();

		Assert.Equal(3, ContentStore.Count);
		Assert.Contains(new ContentKey("alpha", "terrain", "Ore"), ContentStore.Keys);
		Assert.Equal(2, ContentStore.All<TerrainDef>().Count());
		Assert.Single(ContentStore.All<TilesetDef>());
	}

	[Fact]
	public void FullIdMatchesKeyTextForm()
	{
		Arrange();

		var def = ContentStore.Resolve<TerrainDef>("beta:terrain/Ore");

		Assert.Equal("beta:terrain/Ore", def.FullId);
		Assert.Equal(def.FullId, def.Key.ToString());
		Assert.Equal(def.Key.ToString(), def.ToString());
	}

	[Fact]
	public void SameModDuplicateLocalIdIsRejected()
	{
		ContentStore.Add(new TerrainDef("Ore") { SourceMod = _alpha });

		Assert.Throws<InvalidOperationException>(
			() => ContentStore.Add(new TerrainDef("Ore") { SourceMod = _alpha }));
	}

	[Fact]
	public void UnscopedContentResolvesWithoutAmbiguity()
	{
		ContentStore.Add(new TerrainDef("ground") { SourceMod = _alpha });

		Assert.Equal("ground", ContentStore.Resolve<TerrainDef>("ground").Id);
		Assert.Equal("alpha:terrain/ground", ContentStore.Resolve("ground").Key.ToString());
	}

	[Fact]
	public void LookupsWorkAfterSeal()
	{
		Arrange();
		ContentStore.Seal();

		Assert.True(ContentStore.IsSealed);
		Assert.Equal(2, ContentStore.Resolve<TerrainDef>("Ore", Referrer(_beta)).MoveCost);
		Assert.Throws<ContentResolutionException>(() => ContentStore.Resolve<TerrainDef>("Ore"));
	}

	[Fact]
	public void QualifiedReferenceIgnoresTheReferrersMod()
	{
		Arrange();

		Assert.Equal(1, ContentStore.Resolve<TerrainDef>("alpha:terrain/Ore", Referrer(_beta)).MoveCost);
		Assert.Equal(2, ContentStore.Get<TerrainDef>("Ore", Referrer(_beta))!.MoveCost);
	}

	private static TerrainDef Referrer(ModInfo mod) => new("__referrer") { SourceMod = mod };
}