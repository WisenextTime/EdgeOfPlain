using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Net.Client;
using EdgeOfPlain.Core.Net.Server;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class NetTurnTests
{
	private static readonly string _checksum = new('A', 64);

	[Fact]
	public void ServerWaitsForEveryPlayerAndBroadcastsIdenticalOrderedFrames()
	{
		var port = GetFreePort();
		using var server = new ServerNetNode(port, checksum: _checksum);
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

		first.Submit(1, [1, 2]);
		Assert.Throws<InvalidOperationException>(() => first.Submit(2, [9]));
		PumpFor(TimeSpan.FromMilliseconds(60), server, first, second);
		Assert.False(first.TryDequeueFrame(out _));

		second.Submit(1, [3]);
		PumpUntil(() => first.AvailableFrames == 1 && second.AvailableFrames == 1, server, first, second);
		Assert.True(first.TryDequeueFrame(out var a));
		Assert.True(second.TryDequeueFrame(out var b));
		Assert.Equal(1, a.Tick);
		Assert.Equal(1, b.Tick);
		Assert.Equal(2, a.Players.Count);
		Assert.Equal(0, a.Players[0].PlayerId);
		Assert.Equal(1, a.Players[1].PlayerId);
		Assert.Equal(new byte[] { 1, 2 }, a.Players[first.PlayerId!.Value].Payload);
		Assert.Equal(new byte[] { 3 }, a.Players[second.PlayerId!.Value].Payload);
		Assert.Equal(a.Players[0].Payload, b.Players[0].Payload);
		Assert.Equal(a.Players[1].Payload, b.Players[1].Payload);
		Assert.Throws<ArgumentOutOfRangeException>(() => first.Submit(1, [9]));

		first.Submit(2, []);
		second.Submit(2, [4, 5]);
		PumpUntil(() => first.AvailableFrames == 1 && second.AvailableFrames == 1, server, first, second);
		Assert.True(first.TryDequeueFrame(out var next));
		Assert.Equal(2, next.Tick);
		Assert.Empty(next.Players[first.PlayerId!.Value].Payload);
		Assert.Equal(new byte[] { 4, 5 }, next.Players[second.PlayerId!.Value].Payload);
		Assert.Equal(3, server.NextTick);
	}

	[Fact]
	public void RejectsMismatchedChecksumAndDuplicatePlayerName()
	{
		var port = GetFreePort();
		using var server = new ServerNetNode(port, checksum: _checksum);
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
		var port = GetFreePort();
		using var server = new ServerNetNode(port, checksum: _checksum);
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

	private static int GetFreePort()
	{
		using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
		return ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
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