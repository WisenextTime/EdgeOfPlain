using System;
using System.Collections.Generic;

namespace EdgeOfPlain.Core.Game.Commands;

public interface ICommand { }

public sealed class CommandBus
{
	private readonly Dictionary<Type, ICommandQueue> _byType = new();
	private readonly List<ICommandQueue> _queues = [];
	private readonly List<Type> _pendingTypes = [];

	public IReadOnlyList<Type> PendingTypes => _pendingTypes;

	public int PendingCount
	{
		get
		{
			var total = 0;
			foreach (var queue in _queues) total += queue.Count;
			return total;
		}
	}

	public void Enqueue<TCommand>(in TCommand command) where TCommand : struct, ICommand
	{
		var queue = GetOrCreate<TCommand>();
		if (queue.Count == 0) _pendingTypes.Add(typeof(TCommand));
		queue.Add(command);
	}

	public int Count<TCommand>() where TCommand : struct, ICommand =>
		Get<TCommand>()?.Count ?? 0;

	public IReadOnlyList<TCommand> Pending<TCommand>() where TCommand : struct, ICommand
	{
		var queue = Get<TCommand>();
		return queue is null ? Array.Empty<TCommand>() : queue.Items;
	}

	public void Clear()
	{
		foreach (var c in _queues) c.Clear();
		_pendingTypes.Clear();
	}

	public void Clear<TCommand>() where TCommand : struct, ICommand
	{
		var queue = Get<TCommand>();
		if (queue is null || queue.Count == 0) return;
		queue.Clear();
		_pendingTypes.Remove(typeof(TCommand));
	}

	public void Clear<TCommand>(int count) where TCommand : struct, ICommand
	{
		var queue = Get<TCommand>();
		if (queue is null) return;

		if (count >= queue.Count)
		{
			queue.Clear();
			_pendingTypes.Remove(typeof(TCommand));
			return;
		}

		queue.RemoveFirst(count);
	}

	internal void ClearType(Type commandType)
	{
		if (_byType.TryGetValue(commandType, out var queue)) queue.Clear();
		_pendingTypes.Remove(commandType);
	}

	internal int CountType(Type commandType) =>
		_byType.TryGetValue(commandType, out var queue) ? queue.Count : 0;

	private CommandQueue<TCommand>? Get<TCommand>() where TCommand : struct, ICommand =>
		_byType.TryGetValue(typeof(TCommand), out var queue) ? (CommandQueue<TCommand>)queue : null;

	private CommandQueue<TCommand> GetOrCreate<TCommand>() where TCommand : struct, ICommand
	{
		if (_byType.TryGetValue(typeof(TCommand), out var existing)) return (CommandQueue<TCommand>)existing;

		var created = new CommandQueue<TCommand>();
		_byType.Add(typeof(TCommand), created);
		_queues.Add(created);
		return created;
	}

	private interface ICommandQueue
	{
		int Count { get; }

		void Clear();
	}

	private sealed class CommandQueue<TCommand> : ICommandQueue
		where TCommand : struct, ICommand
	{
		public readonly List<TCommand> Items = [];

		public int Count => Items.Count;

		public void Add(in TCommand command) => Items.Add(command);

		public void Clear() => Items.Clear();

		public void RemoveFirst(int count) => Items.RemoveRange(0, count);
	}
}
