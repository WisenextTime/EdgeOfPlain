using FixedMathSharp;

namespace EdgeOfPlain.Core.Game.Waypoint;

public interface IWaypointTarget;

public sealed record GroundTarget(Vector2d Position) : IWaypointTarget;

public sealed record UnitTarget(UnitId Unit) : IWaypointTarget;