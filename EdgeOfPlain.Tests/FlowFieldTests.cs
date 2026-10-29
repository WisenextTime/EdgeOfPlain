using System;
using Arch.Core;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Systems;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class FlowFieldTests : IDisposable
{
	public FlowFieldTests() => ContentStore.Reset();

	public void Dispose() => ContentStore.Reset();

	[Fact]
	public void OpenFieldReachesGoalFromEveryTile()
	{
		var map = PathTestMaps.Build(8, 8, (_, _) => PathTestMaps.GroundKind);
		var goal = FlowField.Pack(7, 7);
		var field = new FlowField(map, goal, PassageMask.Ground, MinOverBits(map));

		for (var y = 0; y < 8; y++)
		{
			for (var x = 0; x < 8; x++)
			{
				Assert.Equal((7, 7), WalkToGoal(field, x, y));
			}
		}
	}

	[Fact]
	public void WallSeparatesUnreachableTiles()
	{
		var map = PathTestMaps.Build(5, 5, (x, y) => x == 2 ? PathTestMaps.CliffKind : PathTestMaps.GroundKind);
		var goal = FlowField.Pack(4, 2);
		var field = new FlowField(map, goal, PassageMask.Ground, MinOverBits(map));

		Assert.Null(field.Next(0, 2));
		Assert.NotNull(field.Next(3, 2));
		Assert.Equal((4, 2), WalkToGoal(field, 3, 2));
	}

	[Fact]
	public void FlowPrefersLowerCostRoute()
	{
		var map = PathTestMaps.Build(
			4, 4,
			(x, y) => y == 1 && x >= 1 && x <= 2 ? PathTestMaps.MudKind : PathTestMaps.GroundKind);
		var goal = FlowField.Pack(3, 1);
		var field = new FlowField(map, goal, PassageMask.Ground, MinOverBits(map));

		Assert.NotEqual((1, 1), field.Next(0, 1));
		Assert.Equal((0, 0), field.Next(0, 1));
	}

	[Fact]
	public void MultiMaskUnitPassesWaterWithAirEntry()
	{
		var map = PathTestMaps.Build(4, 4, (_, _) => PathTestMaps.WaterKind);
		var goal = FlowField.Pack(3, 3);
		var field = new FlowField(map, goal, PassageMask.Ground | PassageMask.Air, MinOverBits(map));

		Assert.NotNull(field.Next(0, 0));
		Assert.Equal((3, 3), WalkToGoal(field, 0, 0));
	}

	[Fact]
	public void MultiMaskUnitBlockedWhenAirNotDeclared()
	{
		var map = PathTestMaps.Build(4, 4, (_, _) => PathTestMaps.PondKind);
		var goal = FlowField.Pack(3, 3);
		var field = new FlowField(map, goal, PassageMask.Ground | PassageMask.Air, MinOverBits(map));

		Assert.Null(field.Next(0, 0));
	}

	[Fact]
	public void DiagonalMoveRequiresBothSideTiles()
	{
		var map = PathTestMaps.Build(
			3, 3,
			(x, y) => (x, y) == (1, 0) ? PathTestMaps.CliffKind : PathTestMaps.GroundKind);
		var goal = FlowField.Pack(2, 2);
		var field = new FlowField(map, goal, PassageMask.Ground, MinOverBits(map));

		Assert.NotEqual((1, 1), field.Next(0, 0));
		Assert.Equal((0, 1), field.Next(0, 0));
	}

	[Fact]
	public void SameInputProducesIdenticalField()
	{
		var map = PathTestMaps.Build(6, 6, (x, y) => (x + y) % 3 == 0 ? PathTestMaps.MudKind : PathTestMaps.GroundKind);
		var goal = FlowField.Pack(5, 5);
		var first = new FlowField(map, goal, PassageMask.Ground, MinOverBits(map));
		var second = new FlowField(map, goal, PassageMask.Ground, MinOverBits(map));

		for (var y = 0; y < 6; y++)
		{
			for (var x = 0; x < 6; x++)
			{
				Assert.Equal(first.Next(x, y), second.Next(x, y));
				Assert.Equal(first.Distance(x, y), second.Distance(x, y));
			}
		}
	}

	[Fact]
	public void OccupancyBlocksGroundButAirPasses()
	{
		var map = PathTestMaps.Build(5, 5, (_, _) => PathTestMaps.GroundKind);
		using var world = World.Create();
		var pathfinding = WorldPathfinding.Create(world, map);

		var building = new EntityHandle(world.Create());
		pathfinding.Occupancy.Add(building, new Footprint(0, 0, 0, 0), 2, 2, map);

		var cost = (int x, int y, PassageMask mask) => pathfinding.Cost(x, y, mask);

		var groundField = new FlowField(map, FlowField.Pack(4, 4), PassageMask.Ground, cost);
		Assert.Null(groundField.Next(2, 2));
		Assert.Equal((4, 4), WalkToGoal(groundField, 0, 0));

		var airField = new FlowField(map, FlowField.Pack(4, 4), PassageMask.Air, cost);
		Assert.NotNull(airField.Next(2, 2));
		Assert.Equal((4, 4), WalkToGoal(airField, 2, 2));
	}

	private static Func<int, int, PassageMask, int> MinOverBits(GameMap map)
		=> (x, y, mask) =>
		{
			var best = 0;

			for (var bit = 1; bit <= 8; bit <<= 1)
			{
				if ((mask & (PassageMask)bit) == 0) continue;

				var cost = map.GetMoveCost(x, y, (PassageMask)bit);

				if (cost > 0) best = best == 0 ? cost : Math.Min(best, cost);
			}

			return best;
		};

	private static (int X, int Y) WalkToGoal(FlowField field, int startX, int startY)
	{
		var (x, y) = (startX, startY);

		while (true)
		{
			var next = field.Next(x, y);
			if (next is null) return (x, y);

			(x, y) = next.Value;
		}
	}
}
