using System;
using System.Collections.Generic;
using Arch.Core;
using EdgeOfPlain.Core.Content;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Components;
using EdgeOfPlain.Core.Game.Systems;
using Xunit;

namespace EdgeOfPlain.Tests;

[Collection("ComponentManager")]
public sealed class CommandRegistryTests : IDisposable
{
	private readonly struct PingCommand(int value) : ICommand
	{
		public readonly int Value = value;
	}

	private sealed class PingApplier(List<int> sink) : ICommandApplier<PingCommand>
	{
		public void Apply(World world, in PingCommand command) => sink.Add(command.Value);
	}

	private sealed class CountingQueue : IQueueSystem
	{
		public int Calls;

		public void Operation(QueueWorld world) => Calls++;
	}

	private sealed class TestMod : Mod
	{
		public override ModInfo ModInfo { get; } = new("testcmd", new Version(1, 0, 0));
	}

	public CommandRegistryTests()
	{
		CommandRegistry.Reset();

		if (ComponentManager.IsSealed) return;

		ComponentManager.StageComponent(HealthComponent.CreateComponent, "test");
		ComponentManager.StageComponent(TransformComponent.CreateComponent, "test");
		ComponentManager.Seal();
	}

	public void Dispose() => CommandRegistry.Reset();

	[Fact]
	public void ModContextRegistersAnApplierThatDispatchesCommands()
	{
		var seen = new List<int>();
		using var context = new ModContext(new TestMod());
		context.AddApplier(new PingApplier(seen));

		var bus = new CommandBus();
		var dispatcher = new CommandDispatcher(bus);
		using var world = World.Create();

		bus.Enqueue(new PingCommand(7));

		Assert.Equal(1, dispatcher.ApplyAll(world));
		Assert.Equal([7], seen);
		Assert.Empty(bus.PendingTypes);
	}

	[Fact]
	public void RegisteringASecondApplierForTheSameCommandThrows()
	{
		using var first = new ModContext(new TestMod());
		first.AddApplier(new PingApplier([]));

		using var second = new ModContext(new TestMod());

		var exception = Assert.Throws<InvalidOperationException>(() => second.AddApplier(new PingApplier([])));
		Assert.Contains("already registered", exception.Message);
	}

	[Fact]
	public void SealedRegistryRejectsFurtherRegistration()
	{
		CommandRegistry.Seal();

		using var context = new ModContext(new TestMod());

		Assert.True(CommandRegistry.IsSealed);
		Assert.Throws<InvalidOperationException>(() => context.AddApplier(new PingApplier([])));
		Assert.Throws<InvalidOperationException>(() => context.AddQueue(new CountingQueue()));
	}

	[Fact]
	public void GameWorldRunsQueuesRegisteredByMods()
	{
		var queue = new CountingQueue();
		using var context = new ModContext(new TestMod());
		context.AddQueue(queue);

		using var game = new GameWorld(GameMap.Build(new Map(2, 2)));

		game.RunQueues();
		game.Step();
		game.Step();

		Assert.Equal(3, queue.Calls);
	}

	[Fact]
	public void FailedModContextReturnsItsRegistrations()
	{
		var context = new ModContext(new TestMod());
		context.AddApplier(new PingApplier([]));
		context.AddQueue(new CountingQueue());

		Assert.Equal(1, CommandRegistry.ApplierCount);
		Assert.Equal(1, CommandRegistry.QueueCount);

		context.Return();

		Assert.Equal(0, CommandRegistry.ApplierCount);
		Assert.Equal(0, CommandRegistry.QueueCount);
		Assert.Null(CommandRegistry.FindApplier(typeof(PingCommand)));
	}

	[Fact]
	public void BaseModRegistersItsQueuesAndAppliers()
	{
		ComponentManager.Reset();
		CommandRegistry.Reset();

		var mod = new Base();
		using var context = new ModContext(mod);
		mod.InitMod(context);

		Assert.Equal(3, CommandRegistry.ApplierCount);
		Assert.Single(CommandRegistry.Queues);
		Assert.NotNull(CommandRegistry.FindApplier(typeof(SpawnUnitCommand)));
		Assert.NotNull(CommandRegistry.FindApplier(typeof(DeleteUnitCommand)));
		Assert.NotNull(CommandRegistry.FindApplier(typeof(UnitDiedCommand)));
	}

	[Fact]
	public void ResetClearsAppliersQueuesAndSeal()
	{
		using var context = new ModContext(new TestMod());
		context.AddApplier(new PingApplier([]));
		context.AddQueue(new CountingQueue());
		CommandRegistry.Seal();

		CommandRegistry.Reset();

		Assert.False(CommandRegistry.IsSealed);
		Assert.Equal(0, CommandRegistry.ApplierCount);
		Assert.Equal(0, CommandRegistry.QueueCount);
	}
}
