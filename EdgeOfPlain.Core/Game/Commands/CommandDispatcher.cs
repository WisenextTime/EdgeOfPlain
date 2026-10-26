using System;
using System.Collections.Generic;
using Arch.Core;
using EdgeOfPlain.Core.Utils;

namespace EdgeOfPlain.Core.Game.Commands;

public interface ICommandApplier<TCommand> where TCommand : struct, ICommand
{
	void Apply(World world, in TCommand command);
}

public sealed class CommandDispatcher(CommandBus bus)
{
	private const int MaxPasses = 8;

	private readonly Dictionary<Type, Action<World, CommandBus>> _appliers = new();
	private readonly List<Type> _types = [];

	public void Register<TCommand>(ICommandApplier<TCommand> applier) where TCommand : struct, ICommand
	{
		_appliers[typeof(TCommand)] = (world, commands) =>
		{
			var pending = commands.Pending<TCommand>();
			var count = pending.Count;

			for (var i = 0; i < count; i++)
			{
				var command = pending[i];
				applier.Apply(world, in command);
			}

			commands.Clear<TCommand>(count);
		};
	}

	public int ApplyAll(World world)
	{
		var applied = 0;

		for (var pass = 0; pass < MaxPasses; pass++)
		{
			if (bus.PendingTypes.Count == 0) break;

			_types.Clear();
			_types.AddRange(bus.PendingTypes);

			foreach (var type in _types)
			{
				if (_appliers.TryGetValue(type, out var apply))
				{
					applied += bus.CountType(type);
					apply(world, bus);
				}
				else
				{
					LogUtils.LogMessage($"no applier registered for command type '{type.FullName}'", LogUtils.LogType.Warning);
					bus.ClearType(type);
				}
			}
		}

		return applied;
	}

	public void Clear() => bus.Clear();
}
