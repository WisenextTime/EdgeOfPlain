using System;
using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Systems;
using EdgeOfPlain.Core.MathLib;

namespace EdgeOfPlain.Core.Content.Systems.PathFinding;

public sealed class MovementSystem(PathfindingSystem pathfinding) : IFrameSystem
{
	private const float TurnThreshold = 0.05f;
	private const float ArriveRadius = 24f;
	private const float CongestionArriveRadius = 48f;
	private const float AvoidRadius = 64f;
	private const float AvoidWidth = 28f;
	private const float SeparationRadius = 32f;
	private const int StuckFrameLimit = 90;
	private const int MaxRepathAttempts = 3;

	private readonly List<EntityHandle> _arrived = [];
	private readonly List<EntityHandle> _repath = [];
	private readonly Dictionary<EntityHandle, int> _stuckFrames = [];
	private readonly Dictionary<EntityHandle, float> _goalDistances = [];

	public void Operation(World world, float delta)
	{
		if (delta <= 0f) return;

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
			var distance = direction.Length();

			var desired = Vector2.Normalize(direction);
			var (avoidance, avoidanceStrength) = SteerAroundObstacles(world, entity, transform.Position, desired);
			var avoidanceActive = avoidance.LengthSquared() > 0.01f;

			var isLastWaypoint = path.Index == path.Waypoints.Count - 1;

			if (distance <= ArriveRadius || (isLastWaypoint && avoidanceActive && distance <= CongestionArriveRadius))
			{
				path.Index++;

				if (path.Index >= path.Waypoints.Count)
				{
					velocity.Velocity = 0f;
					entity.Set(velocity);
					_stuckFrames.Remove(handle);
					_goalDistances.Remove(handle);
					_arrived.Add(handle);
				}

				continue;
			}

			desired += avoidance;

			desired = Vector2.Normalize(desired.LengthSquared() < 0.0001f ? direction : desired);

			var desiredAngle = MathF.Atan2(desired.Y, desired.X);
			var angleDiff = AngleExtension.Normalize(desiredAngle - transform.Rotation);

			var targetSpeed = velocity.MaxVelocity;

			if (avoidanceActive)
				targetSpeed = Math.Min(targetSpeed, velocity.MaxVelocity * (1f - avoidanceStrength * 0.75f));

			if (isLastWaypoint)
				targetSpeed = Math.Min(targetSpeed, velocity.MaxVelocity * Math.Clamp(distance / 120f, 0.15f, 1f));

			if (!avoidanceActive && velocity.AlignBeforeTurn && MathF.Abs(angleDiff) > TurnThreshold)
			{
				transform.Rotation = AngleExtension.MoveTowardsAngle(
					transform.Rotation,
					desiredAngle,
					velocity.RotationalVelocity * delta);

				velocity.Velocity = Math.Max(velocity.Velocity - velocity.Acceleration * delta, 0f);
			}
			else
			{
				transform.Rotation = AngleExtension.MoveTowardsAngle(
					transform.Rotation,
					desiredAngle,
					velocity.RotationalVelocity * delta);

				velocity.Velocity += Math.Clamp(
					targetSpeed - velocity.Velocity,
					-velocity.Acceleration * delta,
					velocity.Acceleration * delta);

				var heading = new Vector2(MathF.Cos(transform.Rotation), MathF.Sin(transform.Rotation));
				transform.Position += heading * velocity.Velocity * delta;
			}

			entity.Set(transform);
			entity.Set(velocity);

			var goalDistance = Vector2.Distance(transform.Position, path.Waypoints[^1]);

			if (!_goalDistances.TryGetValue(handle, out var previousGoal))
			{
				_goalDistances[handle] = goalDistance;
				previousGoal = goalDistance;
			}

			if (goalDistance < previousGoal - 0.5f)
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
					velocity.Velocity = 0f;
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

	private static (Vector2 Avoidance, float Strength) SteerAroundObstacles(
		World world,
		Entity self,
		Vector2 position,
		Vector2 desired)
	{
		var avoidance = Vector2.Zero;
		var strength = 0f;
		var description = new QueryDescription().WithAll<TransformComponent, VelocityComponent>();

		world.Query(description, (Entity other, ref TransformComponent otherTransform, ref VelocityComponent otherVelocity) =>
		{
			if (other.Id == self.Id) return;

			var offset = otherTransform.Position - position;
			var squared = offset.LengthSquared();

			if (squared < 1f || squared > AvoidRadius * AvoidRadius) return;

			var distance = MathF.Sqrt(squared);

			var radialStrength = 1f - distance / SeparationRadius;

			if (radialStrength > 0f)
			{
				avoidance += Vector2.Normalize(-offset) * radialStrength * 2f;
				strength = MathF.Max(strength, radialStrength);
			}

			var ahead = Vector2.Dot(offset, desired);
			if (ahead <= 0f) return;

			var lateral = offset - desired * ahead;
			if (lateral.Length() >= AvoidWidth) return;

			if (otherVelocity.Velocity > 1f)
			{
				var otherHeading = new Vector2(
					MathF.Cos(otherTransform.Rotation),
					MathF.Sin(otherTransform.Rotation));

				if (Vector2.Dot(otherHeading, desired) > 0.9f) return;
			}

			var lateralLength = lateral.Length();

			var side = new Vector2(-desired.Y, desired.X);
			var cross = desired.X * offset.Y - desired.Y * offset.X;
			if (cross > 0f) side = -side;

			var away = lateralLength < 1f ? side : Vector2.Normalize(-lateral);
			var lateralStrength = (1f - distance / AvoidRadius) * (1f - lateralLength / AvoidWidth);

			avoidance += away * lateralStrength;
			strength = MathF.Max(strength, lateralStrength);
		});

		return (avoidance, strength);
	}
}