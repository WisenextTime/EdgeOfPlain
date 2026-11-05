using System;
using System.Collections.Generic;
using System.IO;
using EdgeOfPlain.Core.Net;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Game.Commands;

public sealed class NetworkCommandInput
{
	private readonly List<ICommand> _commands = [];

	public int Count => _commands.Count;

	public void Clear() => _commands.Clear();

	public void Add<TCommand>(in TCommand command) where TCommand : struct, ICommand, INetSerializable
	{
		CommandRegistry.FindNetwork(typeof(TCommand));
		_commands.Add(command);
	}

	public byte[] Flush(int maxPayloadBytes = BattleRoomOptions.DefaultMaxPayloadBytes)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(maxPayloadBytes, sizeof(ushort));
		if (_commands.Count > ushort.MaxValue) throw new InvalidOperationException("Too many network commands in one frame.");
		var writer = new NetDataWriter();
		writer.Put((ushort)_commands.Count);
		foreach (var command in _commands)
		{
			var entry = CommandRegistry.FindNetwork(command.GetType());
			var data = new NetDataWriter();
			entry.Write(data, command);
			if (data.Length > ushort.MaxValue) throw new InvalidOperationException("Network command is too large.");
			writer.Put(entry.Id);
			writer.Put((ushort)data.Length);
			writer.Put(data.Data, 0, data.Length);
		}
		if (writer.Length > maxPayloadBytes) throw new InvalidOperationException("Command input exceeds the network frame limit.");
		_commands.Clear();
		var result = new byte[writer.Length];
		Array.Copy(writer.Data, result, result.Length);
		return result;
	}
}

public static class NetworkCommands
{
	public static void Enqueue(TurnFrame frame, ICommandSink sink)
	{
		ArgumentNullException.ThrowIfNull(sink);
		var batch = new CommandBatch();
		foreach (var player in frame.Players)
			Decode(player.Payload, batch);
		batch.Apply(sink);
	}

	public static void Validate(byte[] payload) => Decode(payload, new CommandBatch());

	private static void Decode(byte[] payload, CommandBatch batch)
	{
		try
		{
			var reader = new NetDataReader(payload);
			if (reader.AvailableBytes < sizeof(ushort)) throw new InvalidOperationException("Truncated command frame.");
			var count = reader.GetUShort();
			for (var i = 0; i < count; i++)
			{
				if (reader.AvailableBytes < 2 * sizeof(ushort)) throw new InvalidOperationException("Truncated command entry.");
				var entry = CommandRegistry.FindNetwork(reader.GetUShort());
				var length = reader.GetUShort();
				if (reader.AvailableBytes < length) throw new InvalidOperationException("Truncated command payload.");
				var bytes = new byte[length];
				reader.GetBytes(bytes, 0, length);
				var commandReader = new NetDataReader(bytes);
				entry.Read(commandReader, batch);
				if (commandReader.AvailableBytes != 0) throw new InvalidOperationException("Command payload has trailing data.");
			}
			if (reader.AvailableBytes != 0) throw new InvalidOperationException("Command frame has trailing data.");
		}
		catch(Exception exception) when(exception is not OutOfMemoryException)
		{
			throw new InvalidDataException("Invalid network command payload.", exception);
		}
	}

	private sealed class CommandBatch : ICommandSink
	{
		private readonly List<IPendingCommand> _pending = [];

		public void Enqueue<TCommand>(in TCommand command) where TCommand : struct, ICommand
			=> _pending.Add(new PendingCommand<TCommand>(command));

		public void Apply(ICommandSink sink)
		{
			if (sink is GameCore core)
				foreach (var pending in _pending)
					core.ValidateDestination(pending.Value);
			foreach (var pending in _pending) pending.Apply(sink);
		}
	}

	private interface IPendingCommand
	{
		ICommand Value { get; }
		void Apply(ICommandSink sink);
	}

	private sealed record PendingCommand<TCommand>(TCommand Command) : IPendingCommand
		where TCommand : struct, ICommand
	{
		public ICommand Value => Command;
		public void Apply(ICommandSink sink) => sink.Enqueue(Command);
	}
}
