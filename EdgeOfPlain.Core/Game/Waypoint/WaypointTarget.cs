using EdgeOfPlain.Core.Game.Systems;
using FixedMathSharp;
using FixedMathSharp.Geometry;

namespace EdgeOfPlain.Core.Game.Waypoint;

public interface IWaypointTarget;

public sealed record GroundTarget(Vector2d Position) : IWaypointTarget;
public sealed record UnitTarget(EntityHandle Entity) : IWaypointTarget;