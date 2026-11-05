using System;
using Arch.Core;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Net;
using LiteNetLib.Utils;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class NetworkCommandTests : IDisposable
{
	private struct NumberCommand : ICommand, INetSerializable
	{
		public int Value;
		public void Serialize(NetDataWriter writer) => writer.Put(Value);
		public void Deserialize(NetDataReader reader) => Value = reader.GetInt();
	}

	private readonly struct LocalCommand : ICommand;

	private sealed class NumberApplier : ICommandApplier<NumberCommand>
	{
		public void Apply(World world, in NumberCommand command) { }
	}

	private sealed class LocalApplier : ICommandApplier<LocalCommand>
	{
		public void Apply(World world, in LocalCommand command) { }
	}

	public NetworkCommandTests() => CommandRegistry.Reset();
	public void Dispose() => CommandRegistry.Reset();

	[Fact]
	public void RegisteredNetworkCommandsRoundTripIntoBusInPlayerOrder()
	{
		CommandRegistry.RegisterApplier(new LocalApplier());
		CommandRegistry.RegisterApplier(new NumberApplier());
		CommandRegistry.Seal();
		Assert.Single(CommandRegistry.NetworkSignatures);
		Assert.Contains(nameof(NumberCommand), CommandRegistry.NetworkSignatures[0]);

		var first = new NetworkCommandInput();
		first.Add(new NumberCommand { Value = 7 });
		first.Add(new NumberCommand { Value = 9 });
		var second = new NetworkCommandInput();
		second.Add(new NumberCommand { Value = 11 });
		var frame = new TurnFrame(1, [new PlayerCommands(0, first.Flush()), new PlayerCommands(1, second.Flush())]);
		var bus = new CommandBus();
		NetworkCommands.Enqueue(frame, bus);
		Assert.Equal([7, 9, 11], new[]
		{
			bus.Pending<NumberCommand>()[0].Value,
			bus.Pending<NumberCommand>()[1].Value, bus.Pending<NumberCommand>()[2].Value
		});
		Assert.Equal(0, first.Count);
		Assert.Throws<InvalidOperationException>(() => CommandRegistry.FindNetwork(typeof(LocalCommand)));
	}
}