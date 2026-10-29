using System;
using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Content.Systems.PathFinding;

public sealed class MovementSystem : IFrameSystem
{
	private const float TurnThreshold = 0.05f;	private const float ArriveRadius = 4f;	private const float AvoidRadius = 96f;

	public void Operation(World world, float delta)
	{
		if (delta <= 0f) return;

		var pathfinding = WorldPathfinding.TryGet(world);
		if (pathfinding is null || pathfinding.Count == 0) return;

		var arrived = new List<EntityHandle>();

		foreach (var (handle, path) in pathfinding.Paths)
		{
			var entity = handle.Value;
			if (!entity.IsAlive())
			{
				arrived.Add(handle);
				continue;
			}

			if (path.Index >= path.Waypoints.Count)
			{
				arrived.Add(handle);
				continue;
			}

			var transform = entity.Get<TransformComponent>();
			var velocity = entity.Get<VelocityComponent>();

			var waypoint = path.Waypoints[path.Index];
			var direction = waypoint - transform.Position;
			var distance = direction.Length();

			if (distance <= ArriveRadius)
			{
				path.Index++;

				if (path.Index >= path.Waypoints.Count)
					arrived.Add(handle);

				continue;
			}

			var desired = Vector2.Normalize(direction);	
			var avoidance = SteerAroundObstacles(world, entity, transform.Position, desired);
			var avoidanceActive = avoidance.LengthSquared() > 0.01f;

			desired += avoidance;

			desired = Vector2.Normalize(desired.LengthSquared() < 0.0001f ? direction : desired);

			var desiredAngle = MathF.Atan2(desired.Y, desired.X);
			var angleDiff = NormalizeAngle(desiredAngle - transform.Rotation);

			if (!avoidanceActive && velocity.AlignBeforeTurn && MathF.Abs(angleDiff) > TurnThreshold)
			{
				var turn = Math.Clamp(
					angleDiff,
					-velocity.MaxRotationalVelocity * delta,
					velocity.MaxRotationalVelocity * delta);

				transform.Rotation += turn;
				velocity.RotationalVelocity = turn / delta;
			}
			else
			{
				transform.Rotation += Math.Clamp(
					angleDiff,
					-velocity.MaxRotationalVelocity * delta,
					velocity.MaxRotationalVelocity * delta);

				if (avoidanceActive)
					velocity.Velocity = MathF.Max(
						velocity.Velocity - velocity.Acceleration * delta,
						velocity.MaxVelocity * 0.25f);
				else				velocity.Velocity = MathF.Min(
					velocity.Velocity + velocity.Acceleration * delta,
					velocity.MaxVelocity);

				var heading = new Vector2(MathF.Cos(transform.Rotation), MathF.Sin(transform.Rotation));
				transform.Position += heading * velocity.Velocity * delta;
			}

			entity.Set(transform);
			entity.Set(velocity);
		}

		foreach (var handle in arrived) pathfinding.ClearPath(handle);
	}

	private static Vector2 SteerAroundObstacles(World world, Entity self, Vector2 position, Vector2 desired)
	{
		var avoidance = Vector2.Zero;
		var avoidRadiusSq = AvoidRadius * AvoidRadius;
		var description = new QueryDescription().WithAll<TransformComponent>();

		world.Query(description, (Entity other, ref TransformComponent otherTransform) =>
		{
			if (other.Id == self.Id) return;

			var offset = otherTransform.Position - position;
			var ahead = Vector2.Dot(offset, desired);

			if (ahead <= 0f) return;

			var squared = offset.LengthSquared();
			if (squared > avoidRadiusSq) return;

			var lateral = offset - desired * ahead;			
			var lateralLength = lateral.Length();

			var away = lateralLength < 1f ? new Vector2(-desired.Y, desired.X) : Vector2.Normalize(-lateral);

			var strength = 1f - MathF.Sqrt(squared) / AvoidRadius;
			avoidance += away * strength;
		});

		return avoidance;
	}

	private static float NormalizeAngle(float angle)
	{
		while (angle > MathF.PI) angle -= MathF.Tau;
		while (angle < -MathF.PI) angle += MathF.Tau;
		return angle;
	}
}
