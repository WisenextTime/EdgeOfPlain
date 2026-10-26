using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Systems;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class CommandDispatcherTests
{
	private readonly struct DamageCommand(int value) : ICommand
	{
		public readonly int Value = value;
	}

	private readonly struct HealedCommand(int value) : ICommand
	{
		public readonly int Value = value;
	}

	private sealed class Recorder<TCommand>(List<TCommand> sink) : ICommandApplier<TCommand>
		where TCommand : struct, ICommand
	{
		public void Apply(World world, in TCommand command) => sink.Add(command);
	}

	private sealed class HealingApplier(CommandBus bus) : ICommandApplier<DamageCommand>
	{
		public void Apply(World world, in DamageCommand command) => bus.Enqueue(new HealedCommand(command.Value));
	}

	private sealed class RecursiveDamageApplier(CommandBus bus, List<DamageCommand> seen) : ICommandApplier<DamageCommand>
	{
		public void Apply(World world, in DamageCommand command)
		{
			seen.Add(command);
			if (command.Value < 2) bus.Enqueue(new DamageCommand(command.Value + 1));
		}
	}

	[Fact]
	public void ApplyAllDispatchesInEnqueueOrderAndClearsTheBus()
	{
		using var world = World.Create();
		var bus = new CommandBus();
		var dispatcher = new CommandDispatcher(bus);
		var seen = new List<DamageCommand>();
		dispatcher.Register(new Recorder<DamageCommand>(seen));

		bus.Enqueue(new DamageCommand(1));
		bus.Enqueue(new DamageCommand(2));
		bus.Enqueue(new DamageCommand(3));

		Assert.Equal(3, dispatcher.ApplyAll(world));
		Assert.Equal([1, 2, 3], seen.ConvertAll(command => command.Value));
		Assert.Empty(bus.PendingTypes);
		Assert.Equal(0, bus.Count<DamageCommand>());
	}

	[Fact]
	public void CommandsEnqueuedByAnApplierAreAppliedInTheSameCall()
	{
		using var world = World.Create();
		var bus = new CommandBus();
		var dispatcher = new CommandDispatcher(bus);
		var healed = new List<HealedCommand>();
		dispatcher.Register(new HealingApplier(bus));
		dispatcher.Register(new Recorder<HealedCommand>(healed));

		bus.Enqueue(new DamageCommand(7));

		Assert.Equal(2, dispatcher.ApplyAll(world));
		Assert.Equal(7, healed[0].Value);
		Assert.Empty(bus.PendingTypes);
	}

	[Fact]
	public void SameTypeCommandsEnqueuedByAnApplierAreAppliedOnTheNextPass()
	{
		using var world = World.Create();
		var bus = new CommandBus();
		var dispatcher = new CommandDispatcher(bus);
		var seen = new List<DamageCommand>();
		dispatcher.Register(new RecursiveDamageApplier(bus, seen));

		bus.Enqueue(new DamageCommand(1));

		Assert.Equal(2, dispatcher.ApplyAll(world));
		Assert.Equal(2, seen.Count);
		Assert.Empty(bus.PendingTypes);
	}

	[Fact]
	public void UnhandledCommandTypesAreDropped()
	{
		using var world = World.Create();
		var bus = new CommandBus();
		var dispatcher = new CommandDispatcher(bus);

		bus.Enqueue(new DamageCommand(1));

		Assert.Equal(0, dispatcher.ApplyAll(world));
		Assert.Empty(bus.PendingTypes);
	}

	[Fact]
	public void ResetClearsWhateverIsStillPending()
	{
		var bus = new CommandBus();
		var dispatcher = new CommandDispatcher(bus);

		bus.Enqueue(new DamageCommand(1));
		dispatcher.Clear();

		Assert.Empty(bus.PendingTypes);
	}

	[Fact]
	public void UnitDiedApplierDestroysTheEntity()
	{
		using var world = World.Create();
		var doomed = world.Create(new HealthComponent { Health = 0 });
		var survivor = world.Create(new HealthComponent { Health = 4 });

		var bus = new CommandBus();
		var dispatcher = new CommandDispatcher(bus);
		dispatcher.Register(new UnitDiedApplier());

		new DeathQueueSystem().Operation(new QueueWorld(world, bus));

		Assert.Equal(1, dispatcher.ApplyAll(world));
		Assert.False(doomed.IsAlive());
		Assert.True(survivor.IsAlive());
	}
}
