using System;
using Arch.Core;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.PathFinding;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Game;

public class GameWorld : IDisposable
{
	private readonly World _world = World.Create();
	private readonly UnitRegistry _units;

	public GameWorld(GameMap map) : this(map, WorldKey.Primary)
	{
	}

	public GameWorld(GameMap map, WorldKey key)
	{
		Map = map ?? throw new ArgumentNullException(nameof(map));
		if (!key.IsValid) throw new ArgumentOutOfRangeException(nameof(key), key, "World key must be positive.");

		Key = key;
		WorldPathfinding.Create(_world, Map);
		_units = UnitRegistry.Create(_world);
		Dispatcher = new CommandDispatcher(Commands);
	}

	public WorldKey Key { get; }

	public GameMap Map { get; }

	public World World => _world;

	public UnitRegistry Units => _units;

	public CommandBus Commands { get; } = new();

	public CommandDispatcher Dispatcher { get; }

	public WorldPathfinding Pathfinding => WorldPathfinding.TryGet(_world)
	                                       ?? throw new InvalidOperationException(
		                                       "World pathfinding is not registered for this world.");

	public int RunFrameSystems(Fixed64 delta)
	{
		var systems = CommandRegistry.FrameSystems;
		if (systems.Count == 0) return 0;

		foreach (var system in systems) system.Operation(_world, delta);

		return systems.Count;
	}

	public int ApplyCommands() => Dispatcher.ApplyAll(_world);

	public UnitId IdOf(EntityHandle handle) => _units.Register(handle.Value);

	public bool TryGetId(EntityHandle handle, out UnitId id) => _units.TryGet(handle.Value, out id);

	public bool TryResolve(UnitId id, out EntityHandle handle)
	{
		if (_units.TryResolve(_world, id, out var entity))
		{
			handle = new EntityHandle(entity);
			return true;
		}

		handle = default;
		return false;
	}

	public UnitGroup Select(params EntityHandle[] handles)
	{
		var ids = new UnitId[handles.Length];
		for (var i = 0; i < handles.Length; i++) ids[i] = _units.Register(handles[i].Value);
		return new UnitGroup(ids);
	}

	public int CollectEntities(in QueryDescription description, Span<EntityHandle> destination)
	{
		var count = _world.CountEntities(in description);
		if (count == 0) return 0;

		var entities = count <= 256 ? stackalloc Entity[count] : new Entity[count];
		_world.GetEntities(in description, entities);

		var copy = Math.Min(count, destination.Length);
		for (var i = 0; i < copy; i++)
		{
			destination[i] = new EntityHandle(entities[i]);
		}

		return count;
	}

	public int Step(Fixed64 delta)
	{
		RunFrameSystems(delta);
		return ApplyCommands();
	}

	public int Step() => Step(Fixed64.Zero);

	public void Dispose()
	{
		WorldPathfinding.Dispose(_world);
		UnitRegistry.Dispose(_world);
		_world.Dispose();
		GC.SuppressFinalize(this);
	}
}