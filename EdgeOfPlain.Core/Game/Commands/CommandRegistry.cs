using System;
using System.Collections.Generic;
using Arch.Core;
using EdgeOfPlain.Core.Game.Systems;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Game.Commands
{
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

	internal interface ICoreCommandApplierEntry
	{
		Type CommandType { get; }

		void Apply(GameCore core, CommandBus bus);
	}

	internal sealed class CoreCommandApplierEntry<TCommand>(ICoreCommandApplier<TCommand> applier) : ICoreCommandApplierEntry
		where TCommand : struct, ICommand
	{
		public Type CommandType => typeof(TCommand);

		public void Apply(GameCore core, CommandBus bus)
		{
			var pending = bus.Pending<TCommand>();
			var count = pending.Count;

			for (var i = 0; i < count; i++)
			{
				var command = pending[i];
				applier.Apply(core, in command);
			}

			bus.Clear<TCommand>(count);
		}
	}

	internal interface INetCommandEntry
	{
		Type CommandType { get; }
		string Signature { get; }
		ushort Id { get; set; }
		void Write(NetDataWriter writer, ICommand command);
		void Read(NetDataReader reader, ICommandSink sink);
	}

	internal sealed class NetCommandEntry<TCommand> : INetCommandEntry where TCommand : struct, ICommand
	{
		public Type CommandType => typeof(TCommand);
		public string Signature => $"{typeof(TCommand).Assembly.GetName().Name}:{typeof(TCommand).FullName}";
		public ushort Id { get; set; }

		public void Write(NetDataWriter writer, ICommand command)
			=> ((INetSerializable)(TCommand)command).Serialize(writer);

		public void Read(NetDataReader reader, ICommandSink sink)
		{
			INetSerializable command = (INetSerializable)default(TCommand);
			command.Deserialize(reader);
			sink.Enqueue((TCommand)command);
		}
	}

	public static class CommandRegistry
	{
		private static readonly List<ICommandApplierEntry> _appliers = [];
		private static readonly Dictionary<Type, ICommandApplierEntry> _applierByType = [];
		private static readonly List<ICoreCommandApplierEntry> _coreAppliers = [];
		private static readonly Dictionary<Type, ICoreCommandApplierEntry> _coreApplierByType = [];
		private static readonly List<IQueueSystem> _queues = [];
		private static readonly List<IFrameSystem> _frameSystems = [];
		private static readonly Dictionary<Type, INetCommandEntry> _networkByType = [];
		private static readonly List<INetCommandEntry> _networkById = [];

		public static bool IsSealed { get; private set; }

		public static int ApplierCount => _appliers.Count;

		public static int CoreApplierCount => _coreAppliers.Count;

		public static int QueueCount => _queues.Count;

		public static int FrameSystemCount => _frameSystems.Count;
		public static IReadOnlyList<string> NetworkSignatures => _networkById.ConvertAll(entry => entry.Signature);

		internal static IReadOnlyList<ICommandApplierEntry> Appliers => _appliers;

		public static IReadOnlyList<IQueueSystem> Queues => _queues;

		public static IReadOnlyList<IFrameSystem> FrameSystems => _frameSystems;

		internal static void RegisterApplier<TCommand>(ICommandApplier<TCommand> applier)
			where TCommand : struct, ICommand
		{
			ArgumentNullException.ThrowIfNull(applier);
			ThrowIfSealed();

			var commandType = typeof(TCommand);
			object? registered = (object?)_applierByType.GetValueOrDefault(commandType) ??
			                     _coreApplierByType.GetValueOrDefault(commandType);
			if (registered is not null)
				throw new InvalidOperationException(
					$"An applier for command '{commandType.FullName}' is already registered by '{registered.GetType().FullName}'.");

			var entry = new CommandApplierEntry<TCommand>(applier);
			_applierByType.Add(commandType, entry);
			_appliers.Add(entry);
			if (typeof(INetSerializable).IsAssignableFrom(commandType))
				_networkByType.Add(commandType, new NetCommandEntry<TCommand>());
		}

		internal static void RegisterCoreApplier<TCommand>(ICoreCommandApplier<TCommand> applier)
			where TCommand : struct, ICommand
		{
			ArgumentNullException.ThrowIfNull(applier);
			ThrowIfSealed();

			var commandType = typeof(TCommand);
			object? registered = (object?)_applierByType.GetValueOrDefault(commandType) ??
			                     _coreApplierByType.GetValueOrDefault(commandType);
			if (registered is not null)
				throw new InvalidOperationException(
					$"An applier for command '{commandType.FullName}' is already registered by '{registered.GetType().FullName}'.");

			var entry = new CoreCommandApplierEntry<TCommand>(applier);
			_coreApplierByType.Add(commandType, entry);
			_coreAppliers.Add(entry);
			if (typeof(INetSerializable).IsAssignableFrom(commandType))
				_networkByType.Add(commandType, new NetCommandEntry<TCommand>());
		}

		internal static bool TryUnregisterApplier(Type commandType)
		{
			ArgumentNullException.ThrowIfNull(commandType);

			if (!_applierByType.Remove(commandType, out var entry)) return false;

			_appliers.Remove(entry);
			_networkByType.Remove(commandType);
			return true;
		}

		internal static bool TryUnregisterCoreApplier(Type commandType)
		{
			ArgumentNullException.ThrowIfNull(commandType);

			if (!_coreApplierByType.Remove(commandType, out var entry)) return false;

			_coreAppliers.Remove(entry);
			_networkByType.Remove(commandType);
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

		internal static void RegisterFrameSystem(IFrameSystem system)
		{
			ArgumentNullException.ThrowIfNull(system);
			ThrowIfSealed();

			if (_frameSystems.Contains(system))
				throw new InvalidOperationException($"Frame system '{system.GetType().FullName}' is already registered.");

			_frameSystems.Add(system);
		}

		internal static bool TryUnregisterFrameSystem(IFrameSystem system)
		{
			ArgumentNullException.ThrowIfNull(system);
			return _frameSystems.Remove(system);
		}

		internal static ICommandApplierEntry? FindApplier(Type commandType)
			=> _applierByType.GetValueOrDefault(commandType);

		internal static ICoreCommandApplierEntry? FindCoreApplier(Type commandType)
			=> _coreApplierByType.GetValueOrDefault(commandType);

		public static void Seal()
		{
			if (IsSealed) return;

			_networkById.AddRange(_networkByType.Values);
			_networkById.Sort(static (a, b) => string.Compare(a.Signature, b.Signature, StringComparison.Ordinal));
			if (_networkById.Count > ushort.MaxValue)
				throw new InvalidOperationException("Too many network command types.");
			for (var i = 0; i < _networkById.Count; i++)
			{
				if (i > 0 && _networkById[i].Signature == _networkById[i - 1].Signature)
					throw new InvalidOperationException($"Duplicate network command signature '{_networkById[i].Signature}'.");
				_networkById[i].Id = (ushort)i;
			}
			IsSealed = true;
		}

		internal static INetCommandEntry FindNetwork(Type type)
		{
			if (!IsSealed) throw new InvalidOperationException("Command registry is not sealed.");
			return _networkByType.TryGetValue(type, out var entry)
				? entry
				: throw new InvalidOperationException($"Command '{type.FullName}' is not registered for networking.");
		}

		internal static INetCommandEntry FindNetwork(ushort id)
		{
			if (!IsSealed || id >= _networkById.Count)
				throw new InvalidOperationException($"Unknown network command ID {id}.");
			return _networkById[id];
		}

		internal static void Reset()
		{
			_appliers.Clear();
			_applierByType.Clear();
			_coreAppliers.Clear();
			_coreApplierByType.Clear();
			_queues.Clear();
			_frameSystems.Clear();
			_networkByType.Clear();
			_networkById.Clear();
			IsSealed = false;
		}

		private static void ThrowIfSealed()
		{
			if (IsSealed) throw new InvalidOperationException("Command registry is already sealed.");
		}
	}
}