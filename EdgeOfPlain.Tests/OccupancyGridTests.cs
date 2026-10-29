using System;
using Arch.Core;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Systems;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class OccupancyGridTests : IDisposable
{
	public OccupancyGridTests() => ContentStore.Reset();

	public void Dispose() => ContentStore.Reset();

	[Fact]
	public void DefaultFootprintOccupiesOnlyBuildingTile()
	{
		var map = PathTestMaps.Build(5, 5, (_, _) => PathTestMaps.GroundKind);
		var grid = new OccupancyGrid();
		using var world = World.Create();
		var building = new EntityHandle(world.Create());

		grid.Add(building, default, 2, 2, map);

		Assert.True(grid.IsOccupied(2, 2));
		Assert.False(grid.IsOccupied(1, 2));
		Assert.False(grid.IsOccupied(2, 1));
		Assert.False(grid.IsOccupied(3, 2));
		Assert.False(grid.IsOccupied(2, 3));
	}

	[Fact]
	public void ExpandedFootprintOccupies3X3()
	{
		var map = PathTestMaps.Build(5, 5, (_, _) => PathTestMaps.GroundKind);
		var grid = new OccupancyGrid();
		using var world = World.Create();
		var building = new EntityHandle(world.Create());

		grid.Add(building, new Footprint(1, 1, 1, 1), 2, 2, map);

		for (var dy = -1; dy <= 1; dy++)
		{
			for (var dx = -1; dx <= 1; dx++)
			{
				Assert.True(grid.IsOccupied(2 + dx, 2 + dy));
			}
		}

		Assert.False(grid.IsOccupied(0, 0));
		Assert.False(grid.IsOccupied(4, 4));
	}

	[Fact]
	public void FootprintClampsAtMapEdge()
	{
		var map = PathTestMaps.Build(3, 3, (_, _) => PathTestMaps.GroundKind);
		var grid = new OccupancyGrid();
		using var world = World.Create();
		var building = new EntityHandle(world.Create());

		grid.Add(building, new Footprint(1, 1, 1, 1), 0, 0, map);

		Assert.True(grid.IsOccupied(0, 0));
		Assert.True(grid.IsOccupied(1, 0));
		Assert.True(grid.IsOccupied(0, 1));
		Assert.True(grid.IsOccupied(1, 1));
		Assert.False(grid.IsOccupied(2, 2));
	}

	[Fact]
	public void RemoveUnmarksAndBumpsVersion()
	{
		var map = PathTestMaps.Build(5, 5, (_, _) => PathTestMaps.GroundKind);
		var grid = new OccupancyGrid();
		using var world = World.Create();
		var building = new EntityHandle(world.Create());

		grid.Add(building, default, 2, 2, map);
		var versionAfterAdd = grid.Version;

		grid.Remove(building);

		Assert.False(grid.IsOccupied(2, 2));
		Assert.Equal(versionAfterAdd + 1, grid.Version);
	}

	[Fact]
	public void DuplicateTileAddIsIgnored()
	{
		var map = PathTestMaps.Build(5, 5, (_, _) => PathTestMaps.GroundKind);
		var grid = new OccupancyGrid();
		using var world = World.Create();
		var first = new EntityHandle(world.Create());
		var second = new EntityHandle(world.Create());

		grid.Add(first, default, 2, 2, map);
		grid.Add(second, default, 2, 2, map);

		Assert.True(grid.IsOccupied(2, 2));

		grid.Remove(first);
		Assert.True(grid.IsOccupied(2, 2));

		grid.Remove(second);
		Assert.False(grid.IsOccupied(2, 2));
	}
}
