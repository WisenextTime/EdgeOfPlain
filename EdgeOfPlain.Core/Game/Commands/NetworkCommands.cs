using System;
using System.Collections.Generic;
using EdgeOfPlain.Core.Net;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Game.Commands;

public sealed class NetworkCommandInput
{
	private readonly List<ICommand> _commands = [];

	public int Count => _commands.Count;

	public void Add<TCommand>(in TCommand command) where TCommand : struct, ICommand, INetSerializable
	{
		CommandRegistry.FindNetwork(typeof(TCommand));
		_commands.Add(command);
	}

	public byte[] Flush()
	{
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
		if (writer.Length > 4096) throw new InvalidOperationException("Command input exceeds the network frame limit.");
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
		foreach (var player in frame.Players)
		{
			var reader = new NetDataReader(player.Payload);
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
				entry.Read(commandReader, sink);
				if (commandReader.AvailableBytes != 0) throw new InvalidOperationException("Command payload has trailing data.");
			}
			if (reader.AvailableBytes != 0) throw new InvalidOperationException("Command frame has trailing data.");
		}
	}
}