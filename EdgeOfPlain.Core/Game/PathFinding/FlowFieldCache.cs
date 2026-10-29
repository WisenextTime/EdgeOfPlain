using System;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Core.Definitions.Worlds;

namespace EdgeOfPlain.Core.Game.PathFinding;

public sealed class FlowFieldCache(
	GameMap map,
	OccupancyGrid occupancy,
	Func<int, int, PassageMask, int> cost)
{
	private const int MaxFields = 32;

	private readonly Dictionary<(long Goal, PassageMask Mask, int Version), FlowField> _fields = [];
	private readonly LinkedList<FlowField> _order = new();

	public FlowField GetOrCreate(long goalTile, PassageMask mask)
	{
		var version = occupancy.Version;
		CleanStale(version);

		var key = (goalTile, mask, version);

		if (_fields.TryGetValue(key, out var field))
		{
			_order.Remove(field);
			_order.AddFirst(field);
			return field;
		}

		field = new FlowField(map, goalTile, mask, cost);
		_fields.Add(key, field);
		_order.AddFirst(field);

		if (_fields.Count <= MaxFields) return field;
		var oldest = _order.Last;
		_order.RemoveLast();

		foreach (var stale in _fields.Where(pair => ReferenceEquals(pair.Value, oldest?.Value)).ToList())
			_fields.Remove(stale.Key);

		return field;
	}

	private void CleanStale(int version)
	{
		if (_fields.Count == 0) return;

		foreach (var key in _fields.Keys.Where(key => key.Version != version).ToList())
		{
			var field = _fields[key];
			_fields.Remove(key);
			_order.Remove(field);
		}
	}
}
