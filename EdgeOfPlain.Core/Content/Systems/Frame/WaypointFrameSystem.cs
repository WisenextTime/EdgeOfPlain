using System;
using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Systems;
using EdgeOfPlain.Core.Game.Waypoint;

namespace EdgeOfPlain.Core.Content.Systems.Frame;

public sealed class WaypointFrameSystem : IFrameSystem, ICommandApplier<WaypointCommand>
{
	private readonly Dictionary<Entity, Queue<Vector2>> _paths = [];
	public float MoveSpeed { get; set; } = 64f;
	public float ArriveRadius { get; set; } = 0.5f;

	public int TrackedCount => _paths.Count;

	public void Apply(World world, in WaypointCommand command)
	{
		var entity = command.Entity.Value;

		if (!entity.IsAlive()) return;
		if (command.Type != WaypointType.Move) return;
		if (command.Target is not GroundTarget ground) return;

		if (command.AddType == WaypointAddType.Replace || !_paths.TryGetValue(entity, out var path))
		{
			path = [];
			_paths[entity] = path;
		}

		path.Enqueue(ground.Position);
	}

	public void Operation(World world, float delta)
	{
		if (_paths.Count == 0) return;

		List<Entity>? finished = null;

		foreach (var (entity, path) in _paths)
		{
			if (!entity.IsAlive())
			{
				(finished ??= []).Add(entity);
				continue;
			}

			if (path.Count == 0)
			{
				(finished ??= []).Add(entity);
				continue;
			}

			if (delta <= 0f) continue;

			ref var transform = ref entity.Get<TransformComponent>();
			var remaining = MoveSpeed * delta;

			while (remaining > 0f && path.Count > 0)
			{
				var target = path.Peek();
				var offset = target - transform.Position;
				var distance = offset.Length();

				if (distance <= ArriveRadius)
				{
					transform.Position = target;
					path.Dequeue();
					continue;
				}

				var step = MathF.Min(distance, remaining);
				var direction = offset / distance;

				transform.Position += direction * step;
				transform.Rotation = MathF.Atan2(direction.Y, direction.X);
				remaining -= step;

				if (step >= distance) path.Dequeue();
			}

			if (path.Count == 0) (finished ??= []).Add(entity);
		}

		if (finished is null) return;

		foreach (var entity in finished) _paths.Remove(entity);
	}
}
