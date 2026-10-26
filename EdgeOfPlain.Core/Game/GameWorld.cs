using System;
using Arch.Core;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Game;

public class GameWorld : IDisposable
{
	private readonly World _world = World.Create();
	private readonly CommandDispatcher _dispatcher;

	public GameWorld(GameMap map)
	{
		Map = map ?? throw new ArgumentNullException(nameof(map));
		_dispatcher = new CommandDispatcher(Commands);

		_dispatcher.Register(new SpawnUnitApplier());
		_dispatcher.Register(new DeleteUnitApplier());
	}

	public GameMap Map { get; }

	public CommandBus Commands { get; } = new();

	public CommandDispatcher Dispatcher => _dispatcher;

	public QueueWorld QueueView() => new(_world, Commands);

	public int ApplyCommands() => Dispatcher.ApplyAll(_world);

	public void Dispose()
	{
		_world.Dispose();
		GC.SuppressFinalize(this);
	}
}
