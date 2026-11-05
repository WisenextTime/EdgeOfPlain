using System.Collections.Generic;

namespace EdgeOfPlain.Core.Game.PathFinding;

public sealed class UnitGroup(params UnitId[] units)
{
	public List<UnitId> Units { get; } = [.. units];

	public int Count => Units.Count;

	public static UnitGroup Empty => new();
}