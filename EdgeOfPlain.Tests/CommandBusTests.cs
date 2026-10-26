using System;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class CommandBusTests
{
	private struct TestCommand(int value) : ICommand
	{
		public int Value = value;
	}

	private readonly struct OtherCommand(int value) : ICommand
	{
		public readonly int Value = value;
	}

	[Fact]
	public void EnqueueKeepsOrderPerCommandType()
	{
		var bus = new CommandBus();

		bus.Enqueue(new TestCommand(1));
		bus.Enqueue(new TestCommand(2));
		bus.Enqueue(new TestCommand(3));

		var pending = bus.Pending<TestCommand>();
		Assert.Equal(3, pending.Count);
		Assert.Equal(1, pending[0].Value);
		Assert.Equal(2, pending[1].Value);
		Assert.Equal(3, pending[2].Value);
	}

	[Fact]
	public void DifferentCommandTypesDoNotShareAQueue()
	{
		var bus = new CommandBus();

		bus.Enqueue(new TestCommand(1));
		bus.Enqueue(new OtherCommand(42));

		Assert.Equal(1, bus.Count<TestCommand>());
		Assert.Equal(1, bus.Count<OtherCommand>());
		Assert.Equal(42, bus.Pending<OtherCommand>()[0].Value);
	}

	[Fact]
	public void PendingTypesListsFirstEnqueueOrder()
	{
		var bus = new CommandBus();

		bus.Enqueue(new TestCommand(1));
		bus.Enqueue(new OtherCommand(2));
		bus.Enqueue(new TestCommand(3));

		Assert.Equal([typeof(TestCommand), typeof(OtherCommand)], bus.PendingTypes);
	}

	[Fact]
	public void PendingIsEmptyForUnusedType()
	{
		var bus = new CommandBus();

		Assert.Empty(bus.Pending<TestCommand>());
		Assert.Equal(0, bus.Count<TestCommand>());
	}

	[Fact]
	public void EnqueueStoresACopyOfTheCommand()
	{
		var bus = new CommandBus();
		var command = new TestCommand(1);

		bus.Enqueue(in command);
		command.Value = 99;

		Assert.Equal(1, bus.Pending<TestCommand>()[0].Value);
	}

	[Fact]
	public void ClearEmptiesEveryQueue()
	{
		var bus = new CommandBus();
		bus.Enqueue(new TestCommand(1));
		bus.Enqueue(new OtherCommand(2));

		bus.Clear();

		Assert.Equal(0, bus.Count<TestCommand>());
		Assert.Equal(0, bus.Count<OtherCommand>());
		Assert.Empty(bus.PendingTypes);
	}
}
