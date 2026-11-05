using System.Collections.Generic;
using System.IO;
using Arch.Core;

namespace EdgeOfPlain.Core.Game;

public sealed class UnitRegistry
{
	private static readonly Dictionary<World, UnitRegistry> _byWorld = [];

	private readonly Dictionary<UnitId, Entity> _entityById = [];
	private readonly Dictionary<Entity, UnitId> _idByEntity = [];
	private int _nextId = UnitId.None;

	private UnitRegistry()
	{
	}

	public int Count => _entityById.Count;
	internal int LastIssuedId => _nextId;

	internal void Restore(IReadOnlyDictionary<UnitId, Entity> units, int lastIssuedId)
	{
		if (lastIssuedId < 0) throw new InvalidDataException("Invalid unit sequence.");
		Clear();
		foreach (var (id, entity) in units)
		{
			_entityById.Add(id, entity);
			_idByEntity.Add(entity, id);
		}
		_nextId = lastIssuedId;
	}

	public static UnitRegistry Create(World world)
	{
		var registry = new UnitRegistry();
		_byWorld[world] = registry;
		return registry;
	}

	public static UnitRegistry? TryGet(World world) => _byWorld.GetValueOrDefault(world);

	public static UnitRegistry GetOrCreate(World world)
		=> _byWorld.TryGetValue(world, out var registry) ? registry : Create(world);

	public static void Dispose(World world) => _byWorld.Remove(world);

	public UnitId Register(Entity entity)
	{
		if (_idByEntity.TryGetValue(entity, out var existing)) return existing;

		var id = new UnitId(++_nextId);
		_entityById.Add(id, entity);
		_idByEntity.Add(entity, id);
		return id;
	}

	public void Unregister(Entity entity)
	{
		if (!_idByEntity.Remove(entity, out var id)) return;
		_entityById.Remove(id);
	}

	public bool TryGet(UnitId id, out Entity entity) => _entityById.TryGetValue(id, out entity);

	public bool TryGet(Entity entity, out UnitId id) => _idByEntity.TryGetValue(entity, out id);

	public bool TryResolve(World world, UnitId id, out Entity entity)
	{
		if (!_entityById.TryGetValue(id, out entity)) return false;
		if (world.IsAlive(entity)) return true;

		Unregister(entity);
		entity = default;
		return false;
	}

	internal void Clear()
	{
		_entityById.Clear();
		_idByEntity.Clear();
		_nextId = UnitId.None;
	}
}
