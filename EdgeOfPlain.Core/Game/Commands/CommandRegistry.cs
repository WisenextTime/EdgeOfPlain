using System;
using System.Collections.Generic;
using Arch.Core;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Game.Commands;

internal interface ICommandApplierEntry
{
	Type CommandType { get; }

	void Apply(World world, CommandBus bus);
}

internal sealed class CommandApplierEntry<TCommand>(ICommandApplier<TCommand> applier) : ICommandApplierEntry
	where TCommand : struct, ICommand
{
	public Type CommandType => typeof(TCommand);

	public void Apply(World world, CommandBus bus)
	{
		var pending = bus.Pending<TCommand>();
		var count = pending.Count;

		for (var i = 0; i < count; i++)
		{
			var command = pending[i];
			applier.Apply(world, in command);
		}

		bus.Clear<TCommand>(count);
	}
}

public static class CommandRegistry
{
	private static readonly List<ICommandApplierEntry> _appliers = [];
	private static readonly Dictionary<Type, ICommandApplierEntry> _applierByType = [];
	private static readonly List<IQueueSystem> _queues = [];

	private static bool _isSealed;

	public static bool IsSealed => _isSealed;

	public static int ApplierCount => _appliers.Count;

	public static int QueueCount => _queues.Count;

	internal static IReadOnlyList<ICommandApplierEntry> Appliers => _appliers;

	public static IReadOnlyList<IQueueSystem> Queues => _queues;

	internal static void RegisterApplier<TCommand>(ICommandApplier<TCommand> applier)
		where TCommand : struct, ICommand
	{
		ArgumentNullException.ThrowIfNull(applier);
		ThrowIfSealed();

		var commandType = typeof(TCommand);
		if (_applierByType.TryGetValue(commandType, out var existing))
			throw new InvalidOperationException(
				$"An applier for command '{commandType.FullName}' is already registered by '{existing.GetType().FullName}'.");

		var entry = new CommandApplierEntry<TCommand>(applier);
		_applierByType.Add(commandType, entry);
		_appliers.Add(entry);
	}

	internal static bool TryUnregisterApplier(Type commandType)
	{
		ArgumentNullException.ThrowIfNull(commandType);

		if (!_applierByType.Remove(commandType, out var entry)) return false;

		_appliers.Remove(entry);
		return true;
	}

	internal static void RegisterQueue(IQueueSystem queue)
	{
		ArgumentNullException.ThrowIfNull(queue);
		ThrowIfSealed();

		if (_queues.Contains(queue))
			throw new InvalidOperationException($"Queue '{queue.GetType().FullName}' is already registered.");

		_queues.Add(queue);
	}

	internal static bool TryUnregisterQueue(IQueueSystem queue)
	{
		ArgumentNullException.ThrowIfNull(queue);
		return _queues.Remove(queue);
	}

	internal static ICommandApplierEntry? FindApplier(Type commandType)
		=> _applierByType.GetValueOrDefault(commandType);

	public static void Seal()
	{
		if (_isSealed) return;

		_isSealed = true;
	}

	internal static void Reset()
	{
		_appliers.Clear();
		_applierByType.Clear();
		_queues.Clear();
		_isSealed = false;
	}

	private static void ThrowIfSealed()
	{
		if (_isSealed) throw new InvalidOperationException("Command registry is already sealed.");
	}
}
