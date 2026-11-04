using System.Collections.Generic;

namespace EdgeOfPlain.Core.Game.PathFinding;

public sealed class UnitGroup(params EntityHandle[] entities)
{
	public List<EntityHandle> Entities { get; } = [.. entities];
}