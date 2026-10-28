using System.Numerics;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Game.Waypoint;

public interface IWaypointTarget;

public sealed record GroundTarget(Vector2 Position) : IWaypointTarget;
public sealed record UnitTarget(EntityHandle Entity) : IWaypointTarget;