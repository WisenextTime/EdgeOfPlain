using System;
using System.Collections.Generic;
using EdgeOfPlain.Core.Utils;
using Microsoft.Extensions.Logging;

namespace EdgeOfPlain.Core.Game.Commands
{
	public interface ICoreCommandApplier<TCommand> where TCommand : struct, ICommand
	{
		void Apply(GameCore core, in TCommand command);
	}

	public sealed class CoreCommandDispatcher(CommandBus bus)
	{
		private const int MaxPasses = 8;

		private readonly Dictionary<Type, Action<GameCore, CommandBus>> _appliers = new();
		private readonly List<Type> _types = [];

		public void Register<TCommand>(ICoreCommandApplier<TCommand> applier) where TCommand : struct, ICommand
		{
			_appliers[typeof(TCommand)] = (core, commands) =>
			{
				var pending = commands.Pending<TCommand>();
				var count = pending.Count;

				for (var i = 0; i < count; i++)
				{
					var command = pending[i];
					applier.Apply(core, in command);
				}

				commands.Clear<TCommand>(count);
			};
		}

		public int ApplyAll(GameCore core)
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
						apply(core, bus);
					}
					else if (CommandRegistry.FindCoreApplier(type) is { } entry)
					{
						applied += bus.CountType(type);
						entry.Apply(core, bus);
					}
					else
					{
						LogUtils.Logger<CoreCommandDispatcher>()
							.LogWarning("no core applier registered for command type '{TypeFullName}'", type.FullName);
						bus.ClearType(type);
					}
				}
			}

			return applied;
		}

		public void Clear() => bus.Clear();
	}
}
