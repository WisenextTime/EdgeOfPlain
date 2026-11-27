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
using EdgeOfPlain.Core.Net.Ai;
using EdgeOfPlain.Core.Net.Client;
using EdgeOfPlain.Core.Net.Server;
using LiteNetLib.Utils;
using Xunit;

namespace EdgeOfPlain.Tests
{
	[Collection("ComponentManager")]
	public sealed class NetTurnTests : IDisposable
	{
		private static readonly string _checksum = new('A', 64);

		public NetTurnTests()
		{
			CommandRegistry.Reset();
			CommandRegistry.RegisterApplier(new BytesApplier());
			CommandRegistry.RegisterApplier(new TeamProbeApplier());
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
		public void ServerBroadcastsTimerDrivenFramesAndFillsMissingSubmissions()
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
			Assert.Equal("Alice", first.Room.Players.Single(player => player.Id == first.PlayerId).Name);

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
			PumpUntil(() => first.AvailableFrames == 1 && second.AvailableFrames == 1, server, first, second);
			Assert.True(first.TryDequeueFrame(out var a));
			Assert.True(second.TryDequeueFrame(out var b));
			Assert.Equal(1, a.Tick);
			Assert.Equal(1, b.Tick);
			Assert.Equal(2, a.Players.Count);
			Assert.Equal(0, a.Players[0].PlayerId);
			Assert.Equal(1, a.Players[1].PlayerId);
			Assert.Equal(Encode(1, 2), a.Players[first.PlayerId!.Value].Payload);
			Assert.Equal(new byte[] { 0, 0 }, a.Players[second.PlayerId!.Value].Payload);
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
		public void DisconnectKeepsOtherPlayersInTheRoom()
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
			PumpUntil(() => server.PlayerCount == 1, server, first);
			PumpUntil(() => first.Room.Players.Count == 1, server, first);
			Assert.True(first.IsStarted);
			Assert.True(first.PlayerId is not null);
			PumpUntil(() => first.AvailableFrames > 0, server, first);
			Assert.True(first.TryDequeueFrame(out var frame));
			Assert.Single(frame.Players);
			Assert.Equal(first.PlayerId, frame.Players[0].PlayerId);
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

		[Fact]
		public void AdministratorDrivesAiSlotsThroughTheirOwnSubmissions()
		{
			using var server = new ServerNetNode(0, checksum: _checksum);
			using var admin = new ClientNetNode("127.0.0.1", server.LocalPort, _checksum, "Alice");
			PumpUntil(() => admin.Room.Players.Count == 1, server, admin);
			Assert.Equal(admin.PlayerId, admin.Room.AdministratorId);
			Assert.Equal(new byte[] { 0, 0 }, EmptyAiClient.Instance.BuildCommands(1));

			admin.RequestAddAi("Bot");
			PumpUntil(() => admin.Room.Players.Count == 2, server, admin);
			var bot = admin.Room.Players.Single(player => player.Kind == RoomPlayerKind.Ai);
			Assert.Equal("Bot", bot.Name);

			server.StartMatch(new Map(1, 1));
			PumpUntil(() => admin.PendingMap is not null, server, admin);
			using var world = new GameWorld(GameMap.Build(admin.PendingMap!.Value));
			admin.MarkWorldReady(world);
			PumpUntil(() => admin.IsStarted, server, admin);

			admin.Submit(1, Encode(1));
			admin.SubmitAi(bot.Id, 1, Encode(2));
			PumpUntil(() => admin.AvailableFrames == 1, server, admin);

			Assert.True(admin.TryDequeueFrame(out var frame));
			Assert.Equal(2, frame.Players.Count);
			Assert.Equal(admin.PlayerId, frame.Players[0].PlayerId);
			Assert.Equal(Encode(1), frame.Players[0].Payload);
			Assert.Equal(bot.Id, frame.Players[1].PlayerId);
			Assert.Equal(Encode(2), frame.Players[1].Payload);
		}

		[Fact]
		public void FrameTeamsAreAssignedToTeamCommands()
		{
			var time = new ManualNetworkTime();
			var options = new NetworkOptions { TimeProvider = time };
			using var server = new ServerNetNode(0, checksum: _checksum, networkOptions: options);
			using var first = new ClientNetNode("127.0.0.1", server.LocalPort, _checksum, "Alice", options);
			using var second = new ClientNetNode("127.0.0.1", server.LocalPort, _checksum, "Bob", options);
			PumpUntil(() => first.Room.Players.Count == 2 && second.Room.Players.Count == 2, server, first, second);
			Assert.True((first.Team, second.Team) is (0, 1) or (1, 0));

			server.StartMatch(new Map(1, 1));
			PumpUntil(() => first.PendingMap is not null && second.PendingMap is not null, server, first, second);
			using var firstWorld = new GameWorld(GameMap.Build(first.PendingMap!.Value));
			using var secondWorld = new GameWorld(GameMap.Build(second.PendingMap!.Value));
			first.MarkWorldReady(firstWorld);
			second.MarkWorldReady(secondWorld);
			PumpUntil(() => first.IsStarted && second.IsStarted, server, first, second);

			var input = new NetworkCommandInput();
			input.Add(new TeamProbeCommand());
			var payload = input.Flush(ushort.MaxValue);
			first.Submit(1, payload);
			second.Submit(1, payload);
			NetworkTestSession.PumpFor(TimeSpan.FromMilliseconds(40), server, first, second);
			time.Advance(TimeSpan.FromMilliseconds(150));
			PumpUntil(() => first.AvailableFrames > 0 && second.AvailableFrames > 0, server, first, second);

			Assert.True(first.TryDequeueFrame(out var frame));
			var sink = new CapturingSink();
			NetworkCommands.Enqueue(frame, sink);
			Assert.Equal(2, sink.Commands.Count);
			Assert.Equal(frame.Players[0].Team, ((TeamProbeCommand)sink.Commands[0]).Team);
			Assert.Equal(frame.Players[1].Team, ((TeamProbeCommand)sink.Commands[1]).Team);
		}

		[Fact]
		public void MapCacheSuppliesMapsWithoutTransfer()
		{
			using var server = new ServerNetNode(0, checksum: _checksum);
			using var client = new ClientNetNode("127.0.0.1", server.LocalPort, _checksum, "Alice")
			{
				MapCache = new StubMapCache(new Map(3, 2)),
			};
			PumpUntil(() => client.PlayerId is not null, server, client);
			server.StartMatch(new Map(1, 1));
			PumpUntil(() => client.PendingMap is not null, server, client);
			Assert.NotNull(client.PendingMap);
			Assert.Equal(3, client.PendingMap!.Value.Width);
			Assert.Equal(2, client.PendingMap.Value.Height);
		}

		private struct TeamProbeCommand : ICommand, INetSerializable, ITeamCommand
		{
			public int Team { get; set; }
			public void Serialize(NetDataWriter writer) { }
			public void Deserialize(NetDataReader reader) { }
		}

		private sealed class TeamProbeApplier : ICommandApplier<TeamProbeCommand>
		{
			public void Apply(World world, in TeamProbeCommand command) { }
		}

		private sealed class CapturingSink : ICommandSink
		{
			public List<ICommand> Commands { get; } = [];

			public void Enqueue<TCommand>(in TCommand command) where TCommand : struct, ICommand
				=> Commands.Add(command);
		}

		private sealed class StubMapCache(Map source) : IMapCache
		{
			public bool TryGet(WorldKey key, string mapId, string checksum, out Map map)
			{
				map = source;
				return true;
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
}
