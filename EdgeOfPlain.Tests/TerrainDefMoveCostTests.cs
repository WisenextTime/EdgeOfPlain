using System.Collections.Generic;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class TerrainDefMoveCostTests
{
	[Fact]
	public void ExplicitMoveCostEntryWins()
	{
		var terrain = new TerrainDef("t")
		{
			SourceMod = TestMods.Base,
			Passage = PassageMask.Ground,
			MoveCost = 5,
			MoveCosts = new Dictionary<string, int> { ["Ground"] = 2 },
		};

		Assert.Equal(2, terrain.GetMoveCost(PassageMask.Ground));
	}

	[Fact]
	public void FallsBackToMoveCostWhenTerrainAllowsPassage()
	{
		var terrain = new TerrainDef("t")
		{
			SourceMod = TestMods.Base,
			Passage = PassageMask.Water | PassageMask.Air,
			MoveCost = 3,
		};

		Assert.Equal(3, terrain.GetMoveCost(PassageMask.Water));
		Assert.Equal(3, terrain.GetMoveCost(PassageMask.Air));
	}

	[Fact]
	public void ReturnsZeroWhenTerrainDoesNotAllowPassage()
	{
		var terrain = new TerrainDef("t")
		{
			SourceMod = TestMods.Base,
			Passage = PassageMask.Water,
			MoveCost = 3,
		};

		Assert.Equal(0, terrain.GetMoveCost(PassageMask.Ground));
		Assert.Equal(0, terrain.GetMoveCost(PassageMask.Air));
	}

	[Fact]
	public void MoveCostsRoundTripsThroughToml()
	{
		RegistryProbes.Ensure(typeof(TerrainDef));
		ContentSerializer.ResetOptions();

		var terrain = new TerrainDef("t")
		{
			SourceMod = TestMods.Base,
			Passage = PassageMask.Ground | PassageMask.Air,
			MoveCost = 1,
			MoveCosts = new Dictionary<string, int> { ["Air"] = 2 },
		};

		var toml = ContentSerializer.Serialize(terrain);
		var restored = Assert.IsType<TerrainDef>(ContentSerializer.Deserialize<ContentDef>(toml));

		Assert.Equal(2, restored.GetMoveCost(PassageMask.Air));
		Assert.Equal(1, restored.GetMoveCost(PassageMask.Ground));
	}
}
