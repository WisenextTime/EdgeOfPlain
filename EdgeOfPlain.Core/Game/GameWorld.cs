using System;
using Arch.Core;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Game;

public class GameWorld : IDisposable
{
	private readonly World _world = World.Create();

	public GameWorld(GameMap map)
	{
		Map = map ?? throw new ArgumentNullException(nameof(map));
		Dispatcher = new CommandDispatcher(Commands);
	}

	public GameMap Map { get; }

	public CommandBus Commands { get; } = new();

	public CommandDispatcher Dispatcher { get; }

	public QueueWorld QueueView() => new(_world, Commands);

	/// <summary>依次运行已注册的队列系统，把组件状态翻译成命令。</summary>
	public int RunQueues()
	{
		var queues = CommandRegistry.Queues;
		if (queues.Count == 0) return 0;

		var view = QueueView();
		foreach (var queue in queues) queue.Operation(view);

		return Commands.PendingCount;
	}

	public int ApplyCommands() => Dispatcher.ApplyAll(_world);

	/// <summary>跑一帧命令管线：队列 → 应用。</summary>
	public int Step()
	{
		RunQueues();
		return ApplyCommands();
	}

	public void Dispose()
	{
		_world.Dispose();
		GC.SuppressFinalize(this);
	}
}
