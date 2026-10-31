using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Systems;
using EdgeOfPlain.Core.MathLib;
using FixedMathSharp;
using FixedMathSharp.Geometry;

namespace EdgeOfPlain.Core.Content.Systems.PathFinding;

public sealed class MovementSystem(PathfindingSystem pathfinding) : IFrameSystem
{
	private static readonly Fixed64 TurnThreshold = (Fixed64)0.05;
	private static readonly Fixed64 ArriveRadius = (Fixed64)24;
	private static readonly Fixed64 CongestionArriveRadius = (Fixed64)48;
	private static readonly Fixed64 AvoidRadius = (Fixed64)64;
	private static readonly Fixed64 AvoidWidth = (Fixed64)28;
	private static readonly Fixed64 SeparationRadius = (Fixed64)32;
	private const int StuckFrameLimit = 90;
	private const int MaxRepathAttempts = 3;

	private readonly List<EntityHandle> _arrived = [];
	private readonly List<EntityHandle> _repath = [];
	private readonly Dictionary<EntityHandle, int> _stuckFrames = [];
	private readonly Dictionary<EntityHandle, Fixed64> _goalDistances = [];

	public void Operation(World world, Fixed64 delta)
	{
		if (delta <= Fixed64.Zero) return;

		var state = WorldPathfinding.TryGet(world);
		if (state is null || state.Count == 0) return;

		foreach (var (handle, path) in state.Paths)
		{
			var entity = handle.Value;
			if (!entity.IsAlive() || path.Index >= path.Waypoints.Count)
			{
				_stuckFrames.Remove(handle);
				_goalDistances.Remove(handle);
				_arrived.Add(handle);
				continue;
			}

			var transform = entity.Get<TransformComponent>();
			var velocity = entity.Get<VelocityComponent>();

			var waypoint = path.Waypoints[path.Index];
			var direction = waypoint - transform.Position;
			var distance = direction.Magnitude;

			var desired = direction.MagnitudeSquared > Fixed64.Zero ? direction.Normalized : Vector2d.Zero;
			var (avoidance, avoidanceStrength) = SteerAroundObstacles(world, entity, transform.Position, desired);
			var avoidanceActive = avoidance.MagnitudeSquared > (Fixed64)0.01;

			var isLastWaypoint = path.Index == path.Waypoints.Count - 1;

			if (distance <= ArriveRadius || (isLastWaypoint && avoidanceActive && distance <= CongestionArriveRadius))
			{
				path.Index++;

				if (path.Index >= path.Waypoints.Count)
				{
					velocity.Velocity = Fixed64.Zero;
					entity.Set(velocity);
					_stuckFrames.Remove(handle);
					_goalDistances.Remove(handle);
					_arrived.Add(handle);
				}

				continue;
			}

			desired += avoidance;

			desired = (desired.MagnitudeSquared < (Fixed64)0.0001 ? direction : desired).Normalized;

			var desiredAngle = FixedMath.Atan2(desired.Y, desired.X);
			var angleDiff = AngleExtension.Normalize(desiredAngle - transform.Rotation);

			var targetSpeed = velocity.MaxVelocity;

			if (avoidanceActive)
				targetSpeed = FixedMath.Min(targetSpeed, velocity.MaxVelocity * (Fixed64.One - avoidanceStrength * (Fixed64)0.75));

			if (isLastWaypoint)
				targetSpeed = FixedMath.Min(targetSpeed, velocity.MaxVelocity * FixedMath.Clamp(distance / (Fixed64)120, (Fixed64)0.15, Fixed64.One));

			if (!avoidanceActive && velocity.AlignBeforeTurn && FixedMath.Abs(angleDiff) > TurnThreshold)
			{
				transform.Rotation = AngleExtension.MoveTowardsAngle(
					transform.Rotation,
					desiredAngle,
					velocity.RotationalVelocity * delta);

				velocity.Velocity = FixedMath.Max(velocity.Velocity - velocity.Acceleration * delta, Fixed64.Zero);
			}
			else
			{
				transform.Rotation = AngleExtension.MoveTowardsAngle(
					transform.Rotation,
					desiredAngle,
					velocity.RotationalVelocity * delta);

				velocity.Velocity += FixedMath.Clamp(
					targetSpeed - velocity.Velocity,
					-velocity.Acceleration * delta,
					velocity.Acceleration * delta);

				var heading = new Vector2d(FixedMath.Cos(transform.Rotation), FixedMath.Sin(transform.Rotation));
				transform.Position += heading * velocity.Velocity * delta;
			}

			entity.Set(transform);
			entity.Set(velocity);

			var goalDistance = transform.Position.Distance(path.Waypoints[^1]);

			if (!_goalDistances.TryGetValue(handle, out var previousGoal))
			{
				_goalDistances[handle] = goalDistance;
				previousGoal = goalDistance;
			}

			if (goalDistance < previousGoal - (Fixed64)0.5)
			{
				_goalDistances[handle] = goalDistance;
				_stuckFrames.Remove(handle);
			}
			else
			{
				if (goalDistance < previousGoal) _goalDistances[handle] = goalDistance;

				_stuckFrames[handle] = _stuckFrames.GetValueOrDefault(handle) + 1;
			}

			if (_stuckFrames.GetValueOrDefault(handle) > StuckFrameLimit)
			{
				if (path.RepathAttempts < MaxRepathAttempts)
				{
					path.RepathAttempts++;
					_stuckFrames.Remove(handle);
					_goalDistances.Remove(handle);
					_repath.Add(handle);
				}
				else
				{
					velocity.Velocity = Fixed64.Zero;
					entity.Set(velocity);
					_stuckFrames.Remove(handle);
					_goalDistances.Remove(handle);
					_arrived.Add(handle);
				}
			}
		}

		foreach (var handle in _arrived) state.ClearPath(handle);

		foreach (var handle in _repath) pathfinding.ComputePath(world, handle);

		_arrived.Clear();
		_repath.Clear();
	}

	private static (Vector2d Avoidance, Fixed64 Strength) SteerAroundObstacles(
		World world,
		Entity self,
		Vector2d position,
		Vector2d desired)
	{
		var avoidance = Vector2d.Zero;
		var strength = Fixed64.Zero;
		var description = new QueryDescription().WithAll<TransformComponent, VelocityComponent>();

		world.Query(description, (Entity other, ref TransformComponent otherTransform, ref VelocityComponent otherVelocity) =>
		{
			if (other.Id == self.Id) return;

			var offset = otherTransform.Position - position;
			var squared = offset.MagnitudeSquared;

			if (squared < (Fixed64)1 || squared > AvoidRadius * AvoidRadius) return;

			var distance = FixedMath.Sqrt(squared);

			var radialStrength = Fixed64.One - distance / SeparationRadius;

			if (radialStrength > Fixed64.Zero)
			{
				avoidance += (-offset).Normalized * radialStrength * (Fixed64)2;
				strength = FixedMath.Max(strength, radialStrength);
			}

			var ahead = Vector2d.Dot(offset, desired);
			if (ahead <= Fixed64.Zero) return;

			var lateral = offset - desired * ahead;
			if (lateral.Magnitude >= AvoidWidth) return;

			if (otherVelocity.Velocity > (Fixed64)1)
			{
				var otherHeading = new Vector2d(
					FixedMath.Cos(otherTransform.Rotation),
					FixedMath.Sin(otherTransform.Rotation));

				if (Vector2d.Dot(otherHeading, desired) > (Fixed64)0.9) return;
			}

			var lateralLength = lateral.Magnitude;

			var side = new Vector2d(-desired.Y, desired.X);
			var cross = desired.X * offset.Y - desired.Y * offset.X;
			if (cross > Fixed64.Zero) side = -side;

			var away = lateralLength < (Fixed64)1 ? side : (-lateral).Normalized;
			var lateralStrength = (Fixed64.One - distance / AvoidRadius) * (Fixed64.One - lateralLength / AvoidWidth);

			avoidance += away * lateralStrength;
			strength = FixedMath.Max(strength, lateralStrength);
		});

		return (avoidance, strength);
	}
}
