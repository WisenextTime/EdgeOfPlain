using System;
using System.Diagnostics;
using System.Threading;
using Arch.Core;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Net;
using EdgeOfPlain.Core.Net.Client;
using EdgeOfPlain.Core.Net.Server;
using FixedMathSharp;
using LiteNetLib.Utils;
using Xunit;

namespace EdgeOfPlain.Tests;

internal struct SyncValue { public int Value; }

internal struct IncrementCommand : ICommand, INetSerializable
{
	public int Amount;
	public void Serialize(NetDataWriter writer) => writer.Put(Amount);
	public void Deserialize(NetDataReader reader) => Amount = reader.GetInt();
}

internal sealed class IncrementApplier : ICommandApplier<IncrementCommand>
{
	public void Apply(World world, in IncrementCommand command)
	{
		var amount = command.Amount;
		world.Query(new QueryDescription().WithAll<SyncValue>(), (ref SyncValue value) => value.Value += amount);
	}
}

internal sealed class NetworkTestSession : IDisposable
{
	public const string Checksum = "network-tests";
	public ServerNetNode Server { get; }
	public ClientNetNode First { get; }
	public ClientNetNode Second { get; }
	public GameCore FirstCore { get; } = new();
	public GameCore SecondCore { get; } = new();
	public static Fixed64 Delta => (Fixed64)0.1;
	public INetNode[] Nodes => [Server, First, Second];

	public NetworkTestSession(NetworkOptions? options = null, int administrator = 0)
	{
		CommandRegistry.Reset();
		CommandRegistry.RegisterApplier(new IncrementApplier());
		CommandRegistry.Seal();
		Server = new ServerNetNode(0, 2, Checksum, options);
		First = new ClientNetNode("127.0.0.1", Server.LocalPort, Checksum, "Alice", options);
		PumpUntil(() => First.PlayerId is not null, Server, First);
		Second = new ClientNetNode("127.0.0.1", Server.LocalPort, Checksum, "Bob", options);
		PumpUntil(() => Second.PlayerId is not null, Nodes);
		Server.SetAdministrator(administrator);
		Server.StartMatch(new Map(2, 2));
		PumpUntil(() => First.PendingMap is not null && Second.PendingMap is not null, Nodes);
		var firstWorld = FirstCore.CreateWorld(WorldKey.Primary, GameMap.Build(First.PendingMap!.Value));
		var secondWorld = SecondCore.CreateWorld(WorldKey.Primary, GameMap.Build(Second.PendingMap!.Value));
		First.BindGameCore(FirstCore, Delta);
		Second.BindGameCore(SecondCore, Delta);
		First.MarkWorldReady(firstWorld);
		Second.MarkWorldReady(secondWorld);
		PumpUntil(() => First.IsStarted && Second.IsStarted, Nodes);
	}

	public static NetworkCommandInput Input(int amount)
	{
		var input = new NetworkCommandInput();
		input.Add(new IncrementCommand { Amount = amount });
		return input;
	}

	public static UnitId AddValue(GameCore core, int value)
	{
		var world = core.PrimaryWorld!;
		return world.Units.Register(world.World.Create(new SyncValue { Value = value }));
	}

	public static int ReadValue(GameCore core, UnitId id)
	{
		Assert.True(core.PrimaryWorld!.TryResolve(id, out var handle));
		return core.PrimaryWorld.World.Get<SyncValue>(handle.Value).Value;
	}

	public void Step(long tick)
	{
		PumpUntil(() => First.AvailableFrames > 0 && Second.AvailableFrames > 0, Nodes);
		Assert.True(First.TryStepWorld(tick, FirstCore, Delta));
		Assert.True(Second.TryStepWorld(tick, SecondCore, Delta));
	}

	public static void PumpUntil(Func<bool> done, params INetNode[] nodes)
	{
		var watch = Stopwatch.StartNew();
		while (watch.Elapsed < TimeSpan.FromSeconds(10))
		{
			foreach (var node in nodes) node.PollEvents();
			if (done()) return;
			Thread.Sleep(1);
		}
		Assert.Fail("Timed out waiting for the network state.");
	}

	public void Dispose()
	{
		First.Dispose();
		Second.Dispose();
		Server.Dispose();
		FirstCore.Dispose();
		SecondCore.Dispose();
		CommandRegistry.Reset();
	}
}

internal sealed class ManualNetworkTime : TimeProvider
{
	private long _timestamp;
	public override long TimestampFrequency => TimeSpan.TicksPerSecond;
	public override long GetTimestamp() => _timestamp;
	public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
}
