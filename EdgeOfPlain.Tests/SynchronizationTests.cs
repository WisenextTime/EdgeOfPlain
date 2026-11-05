using System;
using System.Threading;
using System.Threading.Tasks;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Net;
using Xunit;
using static EdgeOfPlain.Tests.NetworkTestSession;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class SynchronizationTests
{
	private struct LocalEntityLink { public Arch.Core.Entity Target; }

	[Fact]
	public async Task ClientTimesOutWhenTheServerStopsCoordinatingSynchronization()
	{
		var time = new ManualNetworkTime();
		using var room = new NetworkTestSession(new NetworkOptions
		{
			TimeProvider = time, SynchronizationTimeout = TimeSpan.FromSeconds(2),
		});
		var sync = room.Server.SynchronizeAsync(TestContext.Current.CancellationToken);
		PumpUntil(() => room.First.IsSynchronizing, room.Server, room.First);
		time.Advance(TimeSpan.FromSeconds(3));
		room.First.PollEvents();
		Assert.Equal(ClientConnectionStatus.Disconnected, room.First.Status);
		Assert.Equal(ConnectionRejection.SynchronizationTimeout, room.First.Rejection);
		Assert.True(room.FirstCore.IsPaused);
		PumpUntil(() => sync.IsCompleted, room.Server, room.Second);
		await Assert.ThrowsAnyAsync<Exception>(() => sync);
	}

	[Fact]
	public async Task UnsupportedComponentFailsSynchronizationWithoutResumingClients()
	{
		using var room = new NetworkTestSession();
		var world = room.FirstCore.PrimaryWorld!;
		var target = world.World.Create(new SyncValue());
		world.World.Create(new LocalEntityLink { Target = target });
		var sync = room.Server.SynchronizeAsync(TestContext.Current.CancellationToken);
		PumpUntil(() => sync.IsCompleted && room.First.Status == ClientConnectionStatus.Disconnected &&
			room.Second.Status == ClientConnectionStatus.Disconnected, room.Nodes);
		await Assert.ThrowsAsync<InvalidOperationException>(() => sync);
		Assert.IsType<NotSupportedException>(room.First.SynchronizationError);
		Assert.True(room.FirstCore.IsPaused);
		Assert.True(room.SecondCore.IsPaused);
	}

	[Fact]
	public async Task AdministratorStateReplacesAllWorldsAndEveryClientWaitsForResume()
	{
		using var room = new NetworkTestSession(administrator: 1);
		var firstId = AddValue(room.FirstCore, 3);
		var authorityId = AddValue(room.SecondCore, 47);
		room.FirstCore.CreateWorld(new WorldKey(3), GameMap.Build(new Map(1, 1)));
		room.SecondCore.CreateWorld(new WorldKey(2), GameMap.Build(new Map(3, 2)));
		var previousPrimary = room.FirstCore.PrimaryWorld;
		var sync = room.Server.SynchronizeAsync(TestContext.Current.CancellationToken);
		PumpUntil(() => room.First.IsSynchronizing, room.Server, room.First);
		Assert.False(sync.IsCompleted);
		Assert.True(room.FirstCore.IsPaused);
		Assert.False(room.First.TryStepWorld(1, room.FirstCore, Delta));
		Assert.Throws<InvalidOperationException>(() => room.First.Submit(1, new NetworkCommandInput()));
		PumpUntil(() => sync.IsCompleted && room.First.IsStarted && room.Second.IsStarted, room.Nodes);
		await sync;
		Assert.Same(previousPrimary, room.FirstCore.PrimaryWorld);
		Assert.Equal(47, ReadValue(room.FirstCore, authorityId));
		Assert.Equal(47, ReadValue(room.SecondCore, authorityId));
		Assert.Equal(firstId, authorityId);
		Assert.False(room.FirstCore.TryGetWorld(new WorldKey(3), out _));
		Assert.Equal(3, room.FirstCore.GetWorld(new WorldKey(2)).Map.Width);
		Assert.False(room.FirstCore.IsPaused);
		Assert.False(room.SecondCore.IsPaused);
	}

	[Fact]
	public async Task PauseDrainsBroadcastFramesBeforeTakingTheSnapshot()
	{
		using var room = new NetworkTestSession();
		var id = AddValue(room.FirstCore, 0);
		AddValue(room.SecondCore, 0);
		room.First.Submit(1, Input(3));
		room.Second.Submit(1, Input(4));
		PumpUntil(() => room.Server.NextTick == 2, room.Server);
		var sync = room.Server.SynchronizeAsync(TestContext.Current.CancellationToken);
		PumpUntil(() => sync.IsCompleted && room.First.IsStarted && room.Second.IsStarted, room.Nodes);
		await sync;
		Assert.Equal(1, room.First.AppliedTick);
		Assert.Equal(1, room.Second.AppliedTick);
		Assert.Equal(7, ReadValue(room.FirstCore, id));
		Assert.Equal(7, ReadValue(room.SecondCore, id));
		room.First.Submit(2, Input(1));
		room.Second.Submit(2, Input(2));
		room.Step(2);
		Assert.Equal(10, ReadValue(room.FirstCore, id));
		Assert.Equal(10, ReadValue(room.SecondCore, id));
	}

	[Fact]
	public async Task InFlightSubmissionSurvivesPauseAndIsAppliedExactlyOnce()
	{
		using var room = new NetworkTestSession();
		var id = AddValue(room.FirstCore, 0);
		AddValue(room.SecondCore, 90);
		room.First.Submit(1, Input(5));
		var sync = room.Server.SynchronizeAsync(TestContext.Current.CancellationToken);
		PumpUntil(() => sync.IsCompleted && room.First.IsStarted && room.Second.IsStarted, room.Nodes);
		await sync;
		Assert.True(room.First.HasPendingSubmission);
		Assert.False(room.Second.HasPendingSubmission);
		room.Second.Submit(1, Input(2));
		room.Step(1);
		Assert.Equal(7, ReadValue(room.FirstCore, id));
		Assert.Equal(7, ReadValue(room.SecondCore, id));
		var secondSync = room.Server.SynchronizeAsync(TestContext.Current.CancellationToken);
		PumpUntil(() => secondSync.IsCompleted && room.First.IsStarted && room.Second.IsStarted, room.Nodes);
		await secondSync;
		Assert.Equal(7, ReadValue(room.FirstCore, id));
	}

	[Fact]
	public async Task MissingPauseAcknowledgementTimesOutAndFaultsTheTask()
	{
		var time = new ManualNetworkTime();
		using var room = new NetworkTestSession(new NetworkOptions
		{
			TimeProvider = time, SynchronizationTimeout = TimeSpan.FromSeconds(2),
		});
		var sync = room.Server.SynchronizeAsync(TestContext.Current.CancellationToken);
		PumpUntil(() => room.First.IsSynchronizing, room.Server, room.First);
		time.Advance(TimeSpan.FromSeconds(3));
		PumpUntil(() => sync.IsCompleted && room.First.Status == ClientConnectionStatus.Disconnected &&
			room.Second.Status == ClientConnectionStatus.Disconnected, room.Nodes);
		await Assert.ThrowsAsync<TimeoutException>(() => sync);
		Assert.Equal(ConnectionRejection.SynchronizationTimeout, room.First.Rejection);
		Assert.True(room.FirstCore.IsPaused);
		Assert.True(room.SecondCore.IsPaused);
	}

	[Fact]
	public async Task CancellationClosesTheRoomAndCancelsTheTask()
	{
		using var room = new NetworkTestSession();
		using var cancellation = new CancellationTokenSource();
		var sync = room.Server.SynchronizeAsync(cancellation.Token);
		cancellation.Cancel();
		PumpUntil(() => sync.IsCompleted && room.First.Status == ClientConnectionStatus.Disconnected, room.Nodes);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sync);
		Assert.Equal(0, room.Server.PlayerCount);
	}

	[Fact]
	public async Task AdministratorDisconnectCompletesTheTaskWithFailure()
	{
		using var room = new NetworkTestSession();
		var sync = room.Server.SynchronizeAsync(TestContext.Current.CancellationToken);
		PumpUntil(() => room.First.IsSynchronizing, room.Server, room.First);
		room.First.Dispose();
		PumpUntil(() => sync.IsCompleted && room.Second.Status == ClientConnectionStatus.Disconnected,
			room.Server, room.Second);
		await Assert.ThrowsAsync<InvalidOperationException>(() => sync);
		Assert.True(room.SecondCore.IsPaused);
	}
}
