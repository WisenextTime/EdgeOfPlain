using System;
using System.IO;
using System.Linq;
using Arch.Core;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Content.Systems.Applier;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.PathFinding;
using EdgeOfPlain.Core.Game.Waypoint;
using EdgeOfPlain.Core.Net;
using FixedMathSharp;
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
	public void InvalidFrameCannotLeavePartialCommandsOrDuplicateThemOnRetry()
	{
		CommandRegistry.RegisterApplier(new NumberApplier());
		CommandRegistry.Seal();
		var input = new NetworkCommandInput();
		input.Add(new NumberCommand { Value = 42 });
		var malformed = input.Flush().Concat(new byte[] { 255 }).ToArray();
		var frame = new TurnFrame(1, [new PlayerCommands(0, malformed)]);
		var bus = new CommandBus();
		for (var attempt = 0; attempt < 2; attempt++)
		{
			Assert.Throws<InvalidDataException>(() => NetworkCommands.Enqueue(frame, bus));
			Assert.Equal(0, bus.PendingCount);
		}
	}

	[Fact]
	public void InvalidLaterPlayerCannotEnqueueEarlierPlayerCommands()
	{
		CommandRegistry.RegisterApplier(new NumberApplier());
		CommandRegistry.Seal();
		var input = new NetworkCommandInput();
		input.Add(new NumberCommand { Value = 42 });
		var frame = new TurnFrame(1, [new PlayerCommands(0, input.Flush()), new PlayerCommands(1, [])]);
		var bus = new CommandBus();
		Assert.Throws<InvalidDataException>(() => NetworkCommands.Enqueue(frame, bus));
		Assert.Equal(0, bus.PendingCount);
	}

	[Fact]
	public void CombinedGroupOf512UnitsFitsTheDefaultFrameLimit()
	{
		CommandRegistry.RegisterApplier(new PathfindingSystem());
		CommandRegistry.Seal();
		var input = new NetworkCommandInput();
		input.Add(new WaypointCommand(new UnitGroup(Enumerable.Range(1, 512).Select(i => new UnitId(i)).ToArray()),
			WaypointType.Move, WaypointAddType.Replace, new GroundTarget(Vector2d.Zero)) { World = WorldKey.Primary });
		var payload = input.Flush();
		Assert.True(payload.Length < BattleRoomOptions.DefaultMaxPayloadBytes);
		var bus = new CommandBus();
		NetworkCommands.Enqueue(new TurnFrame(1, [new PlayerCommands(0, payload)]), bus);
		Assert.Equal(512, Assert.Single(bus.Pending<WaypointCommand>()).Units.Count);
	}

	[Fact]
	public void FlushUsesTheRoomLimitAndKeepsCommandsWhenTheLimitIsExceeded()
	{
		CommandRegistry.RegisterApplier(new NumberApplier());
		CommandRegistry.Seal();
		var input = new NetworkCommandInput();
		for (var i = 0; i < 600; i++) input.Add(new NumberCommand { Value = i });

		Assert.Throws<InvalidOperationException>(() => input.Flush());
		Assert.Equal(600, input.Count);
		var payload = input.Flush(8192);
		Assert.True(payload.Length > BattleRoomOptions.DefaultMaxPayloadBytes);
		Assert.Equal(0, input.Count);
		var bus = new CommandBus();
		NetworkCommands.Enqueue(new TurnFrame(1, [new PlayerCommands(0, payload)]), bus);
		Assert.Equal(600, bus.Count<NumberCommand>());
		Assert.Equal(599, bus.Pending<NumberCommand>()[599].Value);
	}

	[Fact]
	public void TooManyCommandsCannotWrapTheWireCount()
	{
		CommandRegistry.RegisterApplier(new NumberApplier());
		CommandRegistry.Seal();
		var input = new NetworkCommandInput();
		for (var i = 0; i <= ushort.MaxValue; i++) input.Add(new NumberCommand { Value = i });
		Assert.Throws<InvalidOperationException>(() => input.Flush(1024 * 1024));
		Assert.Equal(ushort.MaxValue + 1, input.Count);
	}

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
