using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Net;
using EdgeOfPlain.Core.Net.Client;
using EdgeOfPlain.Core.Net.Server;
using EdgeOfPlain.Core.Net.Synchronization;
using LiteNetLib;
using LiteNetLib.Utils;
using Xunit;
using static EdgeOfPlain.Tests.NetworkTestSession;
// ReSharper disable AccessToDisposedClosure

namespace EdgeOfPlain.Tests
{
	[Collection("ComponentManager")]
	public sealed class NetworkResilienceTests : IDisposable
	{
		public NetworkResilienceTests()
		{
			CommandRegistry.Reset();
			CommandRegistry.Seal();
		}

		public void Dispose() => CommandRegistry.Reset();

		[Fact]
		public async Task LargeSnapshotIsTransferredAcrossMultiplePollsWithoutLosingBytes()
		{
			var bytes = new byte[SynchronizationProtocol.ChunkBytes * 5 + 17];
			new Random(42).NextBytes(bytes);
			var sender = SnapshotTransmission.Create(7, bytes);
			var begin = sender.Begin();
			var reader = new NetDataReader(begin.Data, 0, begin.Length);
			Assert.Equal((byte)TurnMessage.SnapshotBegin, reader.GetByte());
			Assert.Equal(7, SynchronizationProtocol.ReadId(reader));
			var receiver = new SnapshotReception(reader);
			var complete = false;
			var polls = 0;
			bool sent;
			do
			{
				polls++;
				sent = sender.SendChunks(writer =>
				{
					var chunk = new NetDataReader(writer.Data, 0, writer.Length);
					Assert.Equal((byte)TurnMessage.SnapshotChunk, chunk.GetByte());
					Assert.Equal(7, SynchronizationProtocol.ReadId(chunk));
					complete = receiver.Append(chunk);
				});
			} while (!sent);
			Assert.True(polls > 1);
			Assert.True(complete);
			Assert.Equal(bytes, await receiver.CompleteAsync(TestContext.Current.CancellationToken));
		}

		[Fact]
		public void PendingHandshakesConsumeCapacityAndExpire()
		{
			var time = new ManualNetworkTime();
			var options = new NetworkOptions
			{
				TimeProvider = time, JoinTimeout = TimeSpan.FromSeconds(2), MaxPendingConnections = 1,
			};
			using var server = new ServerNetNode(0, 2, Checksum, options);
			using var pending = new RawClient(server.LocalPort, false);
			PumpUntil(() => server.PendingConnectionCount == 1 && pending.Connected, server, pending);
			using var extra = new RawClient(server.LocalPort, false);
			PumpUntil(() => extra.Rejection != ConnectionRejection.None, server, pending, extra);
			Assert.Equal(ConnectionRejection.RoomFull, extra.Rejection);
			Assert.Equal(1, server.PendingConnectionCount);
			time.Advance(TimeSpan.FromSeconds(3));
			PumpUntil(() => pending.Rejection != ConnectionRejection.None, server, pending);
			Assert.Equal(ConnectionRejection.JoinTimeout, pending.Rejection);
			Assert.Equal(0, server.PendingConnectionCount);
			using var replacement = new ClientNetNode("127.0.0.1", server.LocalPort, Checksum, "Replacement", options);
			PumpUntil(() => replacement.PlayerId is not null, server, replacement);
			Assert.Equal(0, server.PendingConnectionCount);
		}

		[Fact]
		public void LoadingTimeoutClosesClientsThatKeepTheirTransportAlive()
		{
			var time = new ManualNetworkTime();
			var options = new NetworkOptions { TimeProvider = time, LoadingTimeout = TimeSpan.FromSeconds(2) };
			using var server = new ServerNetNode(0, 1, Checksum, options);
			using var client = new ClientNetNode("127.0.0.1", server.LocalPort, Checksum, "Alice", options);
			PumpUntil(() => client.PlayerId is not null, server, client);
			server.StartMatch(new Map(1, 1));
			PumpUntil(() => client.PendingMap is not null, server, client);
			time.Advance(TimeSpan.FromSeconds(3));
			PumpUntil(() => client.Status == ClientConnectionStatus.Disconnected, server, client);
			Assert.Equal(ConnectionRejection.LoadingTimeout, client.Rejection);
			Assert.Equal(0, server.PlayerCount);
		}

		[Fact]
		public void TimerKeepsBroadcastingFramesWhilePlayersAreSilent()
		{
			var time = new ManualNetworkTime();
			using var room = new NetworkTestSession(new NetworkOptions
			{
				TimeProvider = time, TurnTimeout = TimeSpan.FromSeconds(2),
			});
			time.Advance(TimeSpan.FromMilliseconds(250));
			PumpUntil(() => room.First.AvailableFrames == 1 && room.Second.AvailableFrames == 1, room.Nodes);
			Assert.Equal(ClientConnectionStatus.Connected, room.First.Status);
			Assert.Equal(ClientConnectionStatus.Connected, room.Second.Status);
			Assert.Equal(2, room.Server.NextTick);
		}

		[Fact]
		public void TransportConnectionFailureIsVisibleToTheCaller()
		{
			using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
			var port = ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
			using var client = new ClientNetNode("127.0.0.1", port, Checksum, "Alice",
				new NetworkOptions { ConnectRetryDelayMs = 20, MaxConnectAttempts = 1 });
			PumpUntil(() => client.Status == ClientConnectionStatus.Disconnected, client);
			Assert.Equal(ConnectionRejection.ConnectionFailed, client.Rejection);
			Assert.Equal(DisconnectReason.ConnectionFailed, client.LastDisconnectReason);
			Assert.NotNull(client.RejectionReason);
		}

		[Fact]
		public void LateSubmissionsAreDroppedInsteadOfKickingTheSender()
		{
			var time = new ManualNetworkTime();
			var options = new NetworkOptions { TimeProvider = time };
			using var server = new ServerNetNode(0, 2, Checksum, options);
			using var client = new ClientNetNode("127.0.0.1", server.LocalPort, Checksum, "Alice", options);
			using var attacker = new RawClient(server.LocalPort, true);
			PumpUntil(() => client.PlayerId is not null && attacker.Joined, server, client, attacker);
			server.StartMatch(new Map(1, 1));
			PumpUntil(() => client.PendingMap is not null, server, client, attacker);
			using var world = new GameWorld(GameMap.Build(client.PendingMap!.Value));
			client.MarkWorldReady(world);
			PumpUntil(() => client.IsStarted && attacker.Started, server, client, attacker);

			var target = server.NextTick + 3;
			while (server.NextTick < target)
			{
				var before = server.NextTick;
				time.Advance(TimeSpan.FromMilliseconds(150));
				PumpUntil(() => server.NextTick > before, server, client, attacker);
			}

			for (var i = 0; i < 6; i++) attacker.Send(TurnProtocol.Submit(1, "\0\0"u8));
			PumpFor(TimeSpan.FromMilliseconds(50), server, client, attacker);

			Assert.Equal(ConnectionRejection.None, attacker.Rejection);
			Assert.Equal(2, server.PlayerCount);
			Assert.Equal(ClientConnectionStatus.Connected, client.Status);
		}

		[Fact]
		public void BadPacketsKickOnlyTheSenderAfterTheThreshold()
		{
			using var server = new ServerNetNode(0, 2, Checksum);
			using var client = new ClientNetNode("127.0.0.1", server.LocalPort, Checksum, "Alice");
			using var attacker = new RawClient(server.LocalPort, true);
			PumpUntil(() => client.PlayerId is not null && attacker.Joined, server, client, attacker);
			server.StartMatch(new Map(1, 1));
			PumpUntil(() => client.PendingMap is not null, server, client, attacker);
			using var world = new GameWorld(GameMap.Build(client.PendingMap!.Value));
			client.MarkWorldReady(world);
			PumpUntil(() => client.IsStarted && attacker.Started, server, client, attacker);

			for (var i = 0; i < 5; i++) attacker.Send(TurnProtocol.Submit(1, [255]));
			PumpUntil(() => attacker.Rejection == ConnectionRejection.TooManyBadPackets, server, client, attacker);
			Assert.Equal(1, server.PlayerCount);
			Assert.Equal(ClientConnectionStatus.Connected, client.Status);
			PumpUntil(() => client.AvailableFrames > 0, server, client);
		}

		[Fact]
		public async Task SnapshotChunksRejectCorruptionAndInvalidOffsets()
		{
			var bytes = new byte[] { 1, 2, 3, 4 };
			var begin = new NetDataWriter();
			begin.Put(bytes.Length);
			begin.Put(SHA256.HashData(bytes));
			var reception = new SnapshotReception(new NetDataReader(begin.Data, 0, begin.Length));
			var wrongOffset = new NetDataWriter();
			wrongOffset.Put(1);
			TurnProtocol.WritePayload(wrongOffset, bytes);
			Assert.Throws<InvalidDataException>(() => reception.Append(new NetDataReader(wrongOffset.Data, 0, wrongOffset.Length)));
			var corrupted = new NetDataWriter();
			corrupted.Put(0);
			TurnProtocol.WritePayload(corrupted, [1, 2, 3, 5]);
			Assert.True(reception.Append(new NetDataReader(corrupted.Data, 0, corrupted.Length)));
			await Assert.ThrowsAsync<InvalidDataException>(() => reception.CompleteAsync(CancellationToken.None));
		}

		private sealed class RawClient : INetNode
		{
			private readonly NetManager _manager;
			private NetPeer? _peer;
			public bool Connected { get; private set; }
			public bool Joined { get; private set; }
			public bool Started { get; private set; }
			public int Frames { get; private set; }
			public ConnectionRejection Rejection { get; private set; }

			public RawClient(int port, bool join)
			{
				var listener = new EventBasedNetListener();
				listener.PeerConnectedEvent += peer =>
				{
					_peer = peer;
					Connected = true;
					if (join) Send(TurnProtocol.Join(Checksum, "Raw"));
				};
				listener.PeerDisconnectedEvent += (_, info) =>
				{
					Connected = false;
					if (info.AdditionalData is { AvailableBytes: > 0 } data)
					{
						data.GetByte();
						Rejection = TurnProtocol.ReadRejection(data);
					}
				};
				listener.NetworkReceiveEvent += (_, reader, _, _) =>
				{
					try
					{
						switch ((TurnMessage)reader.GetByte())
						{
							case TurnMessage.Welcome: Joined = true; break;
							case TurnMessage.MapInfo: Send(TurnProtocol.Create(TurnMessage.Ready)); break;
							case TurnMessage.Start: Started = true; break;
							case TurnMessage.Frame: Frames++; break;
						}
					}
					finally { reader.Recycle(); }
				};
				_manager = new NetManager(listener);
				_manager.Start();
				_manager.Connect("127.0.0.1", port, TurnProtocol.ConnectionKey);
			}

			public void Send(NetDataWriter writer) => _peer!.Send(writer, TurnProtocol.Channel, TurnProtocol.Delivery);
			public void PollEvents() => _manager.PollEvents();
			public void Dispose() => _manager.Stop();
		}
	}
}
