using System;
using Arch.Core;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Systems;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Game;

public class GameWorld : IDisposable
{
	private readonly World _world = World.Create();

	public GameWorld(GameMap map)
	{
		Map = map ?? throw new ArgumentNullException(nameof(map));
		WorldPathfinding.Create(_world, Map);
		Dispatcher = new CommandDispatcher(Commands);
	}

	public GameMap Map { get; }

	public World World => _world;

	public CommandBus Commands { get; } = new();

	public CommandDispatcher Dispatcher { get; }

	public WorldPathfinding Pathfinding => WorldPathfinding.TryGet(_world)
		?? throw new InvalidOperationException("World pathfinding is not registered for this world.");

	public QueueWorld QueueView() => new(_world, Commands);

	public int RunFrameSystems(Fixed64 delta)
	{
		var systems = CommandRegistry.FrameSystems;
		if (systems.Count == 0) return 0;

		foreach (var system in systems) system.Operation(_world, delta);

		return systems.Count;
	}

	public int RunQueues()
	{
		var queues = CommandRegistry.Queues;
		if (queues.Count == 0) return 0;

		var view = QueueView();
		foreach (var queue in queues) queue.Operation(view);

		return Commands.PendingCount;
	}

	public int ApplyCommands() => Dispatcher.ApplyAll(_world);

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

	public int Step(Fixed64 delta)
	{
		RunFrameSystems(delta);
		RunQueues();
		return ApplyCommands();
	}

	public int Step() => Step(Fixed64.Zero);

	public void Dispose()
	{
		WorldPathfinding.Dispose(_world);
		_world.Dispose();
		GC.SuppressFinalize(this);
	}
}
