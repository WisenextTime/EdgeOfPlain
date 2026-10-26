using System;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Game.Commands;

namespace EdgeOfPlain.Core.Game.Systems;

public interface IQueueSystem
{
	void Operation(QueueWorld world);
}

public readonly struct QueueWorld
{
	private readonly World _world;
	private readonly CommandBus _bus;

	internal QueueWorld(World world, CommandBus bus)
	{
		_world = world;
		_bus = bus;
	}

	public int Count => _world.Size;

	public int Capacity => _world.Capacity;

	public void Enqueue<TCommand>(in TCommand command) where TCommand : struct, ICommand =>
		_bus.Enqueue(in command);

	public static bool IsAlive(in EntityHandle handle)
	{
		var entity = handle.Value;
		return entity.IsAlive();
	}

	public static bool Has<T>(in EntityHandle handle) where T : struct
	{
		var entity = handle.Value;
		return entity.Has<T>();
	}

	public static T Get<T>(in EntityHandle handle) where T : struct
	{
		var entity = handle.Value;
		return entity.Get<T>();
	}

	public static bool TryGet<T>(in EntityHandle handle, out T value) where T : struct
	{
		var entity = handle.Value;
		return entity.TryGet(out value);
	}

	public void Query<T0>(in QueryDescription description, QueryAction<T0> action) where T0 : struct =>
		_world.Query(in description, (Entity entity, ref T0 c0) => action(new EntityHandle(entity), in c0));

	public void Query<T0, T1>(in QueryDescription description, QueryAction<T0, T1> action)
		where T0 : struct
		where T1 : struct =>
		_world.Query(in description,
			(Entity entity, ref T0 c0, ref T1 c1) => action(new EntityHandle(entity), in c0, in c1));

	public int CountEntities(in QueryDescription description) => _world.CountEntities(in description);

	public int CollectEntities(in QueryDescription description, Span<EntityHandle> destination)
	{
		var count = _world.CountEntities(in description);
		if (count == 0) return 0;

		Span<Entity> entities = count <= 256 ? stackalloc Entity[count] : new Entity[count];
		_world.GetEntities(in description, entities);

		var copy = Math.Min(count, destination.Length);
		for (var i = 0; i < copy; i++) destination[i] = new EntityHandle(entities[i]);
		return count;
	}
}

public readonly struct EntityHandle : IEquatable<EntityHandle>, IComparable<EntityHandle>
{
	internal readonly Entity Value;

	internal EntityHandle(Entity value) => Value = value;

	public int Id => Value.Id;

	public int Version => Value.Version;

	public bool Equals(EntityHandle other) =>
		Value.Id == other.Value.Id && Value.Version == other.Value.Version && Value.WorldId == other.Value.WorldId;

	public override bool Equals(object? obj) => obj is EntityHandle other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(Value.WorldId, Value.Id, Value.Version);

	public int CompareTo(EntityHandle other)
	{
		var c = Value.WorldId.CompareTo(other.Value.WorldId);
		if (c != 0) return c;
		c = Value.Id.CompareTo(other.Value.Id);
		return c != 0 ? c : Value.Version.CompareTo(other.Value.Version);
	}

	public static bool operator ==(EntityHandle left, EntityHandle right) => left.Equals(right);

	public static bool operator !=(EntityHandle left, EntityHandle right) => !left.Equals(right);

	public override string ToString() => $"#{Value.Id}v{Value.Version}";
}

public delegate void QueryAction<T0>(in EntityHandle entity, in T0 c0) where T0 : struct;

public delegate void QueryAction<T0, T1>(in EntityHandle entity, in T0 c0, in T1 c1)
	where T0 : struct
	where T1 : struct;
