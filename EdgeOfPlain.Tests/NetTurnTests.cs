using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Arch.Core;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Net;
using EdgeOfPlain.Core.Net.Client;
using EdgeOfPlain.Core.Net.Server;
using LiteNetLib.Utils;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class NetTurnTests : IDisposable
{
	private static readonly string _checksum = new('A', 64);

	public NetTurnTests()
	{
		CommandRegistry.Reset();
		CommandRegistry.RegisterApplier(new BytesApplier());
		CommandRegistry.Seal();
	}

	public void Dispose() => CommandRegistry.Reset();

	private struct BytesCommand : ICommand, INetSerializable
	{
		public byte[] Data;
		public void Serialize(NetDataWriter writer) => writer.Put(Data);
		public void Deserialize(NetDataReader reader) => Data = reader.GetRemainingBytes();
	}

	private sealed class BytesApplier : ICommandApplier<BytesCommand>
	{
		public void Apply(World world, in BytesCommand command) { }
	}

	private static byte[] Encode(params byte[] bytes)
	{
		var input = new NetworkCommandInput();
		input.Add(new BytesCommand { Data = bytes });
		return input.Flush(ushort.MaxValue);
	}

	[Fact]
	public void ServerWaitsForEveryPlayerAndBroadcastsIdenticalOrderedFrames()
	{
		using var server = new ServerNetNode(0, checksum: _checksum);
		var port = server.LocalPort;
		using var first = new ClientNetNode("127.0.0.1", port, _checksum, "Alice");
		using var second = new ClientNetNode("127.0.0.1", port, _checksum, "Bob");

		PumpUntil(() => server.PlayerCount == 2 && first.PlayerId is not null && second.PlayerId is not null,
			server, first, second);
		Assert.NotEqual(first.PlayerId, second.PlayerId);
		Assert.Contains(server.Room.Players, player => player.Name == "Alice");
		Assert.Contains(server.Room.Players, player => player.Name == "Bob");
		PumpUntil(() => first.Room.Players.Count == 2 && second.Room.Players.Count == 2, server, first, second);
		Assert.Equal("Alice", first.Room.Players[first.PlayerId!.Value].Name);

		server.StartMatch(new Map(1, 1));
		PumpUntil(() => first.PendingMap is not null && second.PendingMap is not null, server, first, second);
		Assert.False(server.IsStarted);
		using var firstWorld = new GameWorld(GameMap.Build(first.PendingMap!.Value));
		using var secondWorld = new GameWorld(GameMap.Build(second.PendingMap!.Value));
		first.MarkWorldReady(firstWorld);
		PumpFor(TimeSpan.FromMilliseconds(50), server, first, second);
		Assert.False(server.IsStarted);
		second.MarkWorldReady(secondWorld);
		PumpUntil(() => first.IsStarted && second.IsStarted, server, first, second);

		first.Submit(1, Encode(1, 2));
		Assert.Throws<InvalidOperationException>(() => first.Submit(2, [9]));
		PumpFor(TimeSpan.FromMilliseconds(60), server, first, second);
		Assert.False(first.TryDequeueFrame(out _));

		second.Submit(1, Encode(3));
		PumpUntil(() => first.AvailableFrames == 1 && second.AvailableFrames == 1, server, first, second);
		Assert.True(first.TryDequeueFrame(out var a));
		Assert.True(second.TryDequeueFrame(out var b));
		Assert.Equal(1, a.Tick);
		Assert.Equal(1, b.Tick);
		Assert.Equal(2, a.Players.Count);
		Assert.Equal(0, a.Players[0].PlayerId);
		Assert.Equal(1, a.Players[1].PlayerId);
		Assert.Equal(Encode(1, 2), a.Players[first.PlayerId!.Value].Payload);
		Assert.Equal(Encode(3), a.Players[second.PlayerId!.Value].Payload);
		Assert.Equal(a.Players[0].Payload, b.Players[0].Payload);
		Assert.Equal(a.Players[1].Payload, b.Players[1].Payload);
		Assert.Throws<ArgumentOutOfRangeException>(() => first.Submit(1, [9]));

		first.Submit(2, new NetworkCommandInput());
		second.Submit(2, Encode(4, 5));
		PumpUntil(() => first.AvailableFrames == 1 && second.AvailableFrames == 1, server, first, second);
		Assert.True(first.TryDequeueFrame(out var next));
		Assert.Equal(2, next.Tick);
		Assert.Equal(new byte[] { 0, 0 }, next.Players[first.PlayerId!.Value].Payload);
		Assert.Equal(Encode(4, 5), next.Players[second.PlayerId!.Value].Payload);
		Assert.Equal(3, server.NextTick);
	}

	[Fact]
	public void RejectsMismatchedChecksumAndDuplicatePlayerName()
	{
		using var server = new ServerNetNode(0, checksum: _checksum);
		var port = server.LocalPort;
		using var wrong = new ClientNetNode("127.0.0.1", port, new string('B', 64), "Alice");
		PumpUntil(() => wrong.RejectionReason is not null, server, wrong);
		Assert.Equal("Mod checksum mismatch", wrong.RejectionReason);
		Assert.Equal(0, server.PlayerCount);

		using var first = new ClientNetNode("127.0.0.1", port, _checksum, "Alice");
		PumpUntil(() => first.PlayerId is not null, server, first);
		using var duplicate = new ClientNetNode("127.0.0.1", port, _checksum, "Alice");
		PumpUntil(() => duplicate.RejectionReason is not null, server, first, duplicate);
		Assert.Equal("Invalid or duplicate player name", duplicate.RejectionReason);
		Assert.Single(server.Room.Players);
	}

	[Fact]
	public void DisconnectStopsTheRoomInsteadOfChangingPlayerIds()
	{
		using var server = new ServerNetNode(0, checksum: _checksum);
		var port = server.LocalPort;
		using var first = new ClientNetNode("127.0.0.1", port, _checksum, "Alice");
		using var second = new ClientNetNode("127.0.0.1", port, _checksum, "Bob");

		PumpUntil(() => server.PlayerCount == 2 && first.PlayerId is not null && second.PlayerId is not null,
			server, first, second);
		server.StartMatch(new Map(1, 1));
		PumpUntil(() => first.PendingMap is not null && second.PendingMap is not null, server, first, second);
		using var firstWorld = new GameWorld(GameMap.Build(first.PendingMap!.Value));
		using var secondWorld = new GameWorld(GameMap.Build(second.PendingMap!.Value));
		first.MarkWorldReady(firstWorld);
		second.MarkWorldReady(secondWorld);
		PumpUntil(() => first.IsStarted && second.IsStarted, server, first, second);

		second.Dispose();
		PumpUntil(() => server.PlayerCount == 0 && !first.IsStarted, server, first);
		Assert.Null(first.PlayerId);
		Assert.False(server.IsStarted);
	}

	[Fact]
	public void CustomRoomRulesSupportLargerRostersUnicodeNamesAndFrames()
	{
		var options = new BattleRoomOptions
		{
			MaxPlayers = 12,
			MaxPayloadBytes = 8192,
			MaxMapBytes = 64 * 1024,
			MaxPlayerNameLength = 40,
		};
		var transport = new NetworkOptions { ConnectRetryDelayMs = 100, MaxConnectAttempts = 20 };
		using var server = new ServerNetNode(0, options, _checksum, transport);
		Assert.InRange(server.LocalPort, 1, IPEndPoint.MaxPort);
		var clients = new List<ClientNetNode>();
		var worlds = new List<GameWorld>();
		try
		{
			for (var i = 0; i < options.MaxPlayers; i++)
				clients.Add(new ClientNetNode(IPAddress.Loopback.ToString(), server.LocalPort, _checksum,
					new string('玩', options.MaxPlayerNameLength - 2) + i.ToString("D2"), transport));
			var nodes = clients.ToArray();
			PumpUntil(() => clients.All(client => client.Room.Players.Count == options.MaxPlayers), server, nodes);
			Assert.Equal(options.MaxPlayers, server.PlayerCount);
			foreach (var client in clients)
			{
				Assert.Equal(options, client.Room.Options);
				Assert.Equal(server.Room.Players.ToArray(), client.Room.Players.ToArray());
			}

			server.StartMatch(new Map(1, 1));
			PumpUntil(() => clients.All(client => client.PendingMap is not null), server, nodes);
			foreach (var client in clients)
			{
				var world = new GameWorld(GameMap.Build(client.PendingMap!.Value));
				worlds.Add(world);
				client.MarkWorldReady(world);
			}
			PumpUntil(() => clients.All(client => client.IsStarted), server, nodes);
			Assert.Throws<ArgumentOutOfRangeException>(() => clients[0].Submit(1, new byte[options.MaxPayloadBytes + 1]));
			foreach (var client in clients)
			{
				var payload = new byte[options.MaxPayloadBytes - 3 * sizeof(ushort)];
				Array.Fill(payload, (byte)client.PlayerId!.Value);
				client.Submit(1, Encode(payload));
			}
			PumpUntil(() => clients.All(client => client.AvailableFrames == 1), server, nodes);
			foreach (var client in clients)
			{
				Assert.True(client.TryDequeueFrame(out var frame));
				Assert.Equal(1, frame.Tick);
				Assert.Equal(options.MaxPlayers, frame.Players.Count);
				foreach (var commands in frame.Players)
				{
					Assert.Equal(options.MaxPayloadBytes, commands.Payload.Length);
					Assert.All(commands.Payload.Skip(3 * sizeof(ushort)), value => Assert.Equal((byte)commands.PlayerId, value));
				}
			}
		}
		finally
		{
			foreach (var client in clients) client.Dispose();
			foreach (var world in worlds) world.Dispose();
		}
	}

	[Fact]
	public void CustomNameAndMapLimitsAreEnforcedWithoutChangingRoomState()
	{
		var options = new BattleRoomOptions { MaxPlayerNameLength = 4, MaxMapBytes = 1 };
		using var server = new ServerNetNode(0, options, _checksum);
		using var invalid = new ClientNetNode(IPAddress.Loopback.ToString(), server.LocalPort, _checksum, "Alice");
		PumpUntil(() => invalid.Rejection != ConnectionRejection.None, server, invalid);
		Assert.Equal(ConnectionRejection.InvalidPlayerName, invalid.Rejection);
		Assert.Equal(0, server.PlayerCount);

		using var client = new ClientNetNode(IPAddress.Loopback.ToString(), server.LocalPort, _checksum, "Bob");
		PumpUntil(() => client.PlayerId is not null, server, client);
		Assert.Throws<ArgumentOutOfRangeException>(() => server.StartMatch(new Map(1, 1)));
		Assert.Equal(BattleRoomState.Lobby, server.Room.State);
		Assert.Null(client.PendingMap);
	}

	[Fact]
	public void FullAndLoadingRoomsReturnTypedRejections()
	{
		using var server = new ServerNetNode(0, 1, _checksum);
		using var first = new ClientNetNode(IPAddress.Loopback.ToString(), server.LocalPort, _checksum, "Alice");
		PumpUntil(() => first.PlayerId is not null, server, first);
		using var extra = new ClientNetNode(IPAddress.Loopback.ToString(), server.LocalPort, _checksum, "Bob");
		PumpUntil(() => extra.Rejection != ConnectionRejection.None, server, first, extra);
		Assert.Equal(ConnectionRejection.RoomFull, extra.Rejection);
		Assert.Null(extra.PlayerId);
		Assert.Single(server.Room.Players);

		server.StartMatch(new Map(1, 1));
		using var late = new ClientNetNode(IPAddress.Loopback.ToString(), server.LocalPort, _checksum, "Carol");
		PumpUntil(() => late.Rejection != ConnectionRejection.None, server, first, late);
		Assert.Equal(ConnectionRejection.RoomNotJoinable, late.Rejection);
		Assert.Single(server.Room.Players);
		Assert.Equal(BattleRoomState.Loading, server.Room.State);
	}

	[Fact]
	public void NewMatchAfterDisconnectStartsFromTheFirstTick()
	{
		using var server = new ServerNetNode(0, 1, _checksum);
		for (var match = 0; match < 2; match++)
		{
			using var client = new ClientNetNode(IPAddress.Loopback.ToString(), server.LocalPort, _checksum, "Alice");
			PumpUntil(() => client.PlayerId is not null, server, client);
			Assert.Equal(0, client.PlayerId);
			server.StartMatch(new Map(1, 1));
			PumpUntil(() => client.PendingMap is not null, server, client);
			using var world = new GameWorld(GameMap.Build(client.PendingMap!.Value));
			client.MarkWorldReady(world);
			PumpUntil(() => client.IsStarted, server, client);
			client.Submit(1, new NetworkCommandInput());
			PumpUntil(() => client.AvailableFrames == 1, server, client);
			Assert.Equal(2, server.NextTick);
			client.Dispose();
			Assert.False(client.IsStarted);
			Assert.Null(client.PlayerId);
			Assert.Equal(0, client.AvailableFrames);
			PumpUntil(() => server.PlayerCount == 0, server);
			Assert.Equal(1, server.NextTick);
			Assert.Equal(BattleRoomState.Lobby, server.Room.State);
		}
	}

	private static void PumpUntil(Func<bool> condition, ServerNetNode server, params ClientNetNode[] clients)
	{
		var watch = Stopwatch.StartNew();
		while (watch.Elapsed < TimeSpan.FromSeconds(5))
		{
			server.PollEvents();
			foreach (var client in clients) client.PollEvents();
			if (condition()) return;
			Thread.Sleep(1);
		}

		Assert.Fail("Timed out waiting for the network event.");
	}

	private static void PumpFor(TimeSpan duration, ServerNetNode server, params ClientNetNode[] clients)
	{
		var watch = Stopwatch.StartNew();
		while (watch.Elapsed < duration)
		{
			server.PollEvents();
			foreach (var client in clients) client.PollEvents();
			Thread.Sleep(1);
		}
	}
}
