using System;
using System.Collections.Generic;
using EdgeOfPlain.Core.Definitions.Worlds;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Game.PathFinding;

public sealed class FlowField
{
	private static readonly Fixed64 Sqrt2 = (Fixed64)1.41421356;

	private static readonly (int Dx, int Dy)[] _directions =
	[
		(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1),
	];

	private readonly int _width;
	private readonly int _height;
	private readonly int[] _nextX;
	private readonly int[] _nextY;
	private readonly Fixed64[] _distance;

	public FlowField(
		GameMap map,
		long goalTile,
		PassageMask mask,
		Func<int, int, PassageMask, int> cost)
	{
		Map = map;
		GoalTile = goalTile;
		Mask = mask;
		_width = map.Width;
		_height = map.Height;

		var count = _width * _height;
		_nextX = new int[count];
		_nextY = new int[count];
		_distance = new Fixed64[count];

		Array.Fill(_nextX, -1);
		Array.Fill(_nextY, -1);
		Array.Fill(_distance, Fixed64.MaxValue);

		var (goalX, goalY) = Unpack(goalTile);
		Compute(goalX, goalY, cost);
	}

	public GameMap Map { get; }
	public long GoalTile { get; }
	public PassageMask Mask { get; }

	public (int X, int Y)? Next(int tileX, int tileY)
	{
		if ((uint)tileX >= (uint)_width || (uint)tileY >= (uint)_height) return null;

		var index = Index(tileX, tileY);
		if (_nextX[index] < 0) return null;

		return (_nextX[index], _nextY[index]);
	}

	public Fixed64 Distance(int tileX, int tileY)
	{
		if ((uint)tileX >= (uint)_width || (uint)tileY >= (uint)_height) return Fixed64.MaxValue;
		return _distance[Index(tileX, tileY)];
	}

	public static long Pack(int tileX, int tileY) => (uint)tileX | (long)tileY << 32;

	public static (int X, int Y) Unpack(long tile) => ((int)tile, (int)(tile >> 32));

	private int Index(int x, int y) => y * _width + x;

	private void Compute(int goalX, int goalY, Func<int, int, PassageMask, int> cost)
	{
		var start = Index(goalX, goalY);
		_distance[start] = (Fixed64)0;

		var queue = new PriorityQueue<(int X, int Y), (Fixed64 Distance, int Tie)>();
		queue.Enqueue((goalX, goalY), ((Fixed64)0, 0));

		var tie = 0;

		while (queue.Count > 0)
		{
			queue.TryDequeue(out var node, out var priority);
			var (x, y) = node;

			if (priority.Distance > _distance[Index(x, y)]) continue;

			foreach (var (dx, dy) in _directions)
			{
				var nx = x + dx;
				var ny = y + dy;

				if ((uint)nx >= (uint)_width || (uint)ny >= (uint)_height) continue;

				Fixed64 moveCost;

				if (dx != 0 && dy != 0)
				{
					var destination = cost(nx, ny, Mask);
					var sideA = cost(nx, y, Mask);
					var sideB = cost(x, ny, Mask);

					if (destination <= 0 || sideA <= 0 || sideB <= 0) continue;

					moveCost = Sqrt2 * FixedMath.Max(FixedMath.Max((Fixed64)sideA, (Fixed64)sideB), (Fixed64)destination);
				}
				else
				{
					moveCost = (Fixed64)cost(nx, ny, Mask);
				}

				if (moveCost <= Fixed64.Zero) continue;

				var newDistance = _distance[Index(x, y)] + moveCost;
				var neighbor = Index(nx, ny);

				if (newDistance >= _distance[neighbor]) continue;

				_distance[neighbor] = newDistance;
				_nextX[neighbor] = x;
				_nextY[neighbor] = y;
				queue.Enqueue((nx, ny), (newDistance, tie++));
			}
		}
	}
}
