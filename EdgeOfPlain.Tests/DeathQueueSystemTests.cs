using Arch.Core;
using EdgeOfPlain.Core.Content.Systems;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Content.Systems.Queues;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Systems;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class DeathQueueSystemTests
{
	[Fact]
	public void EnqueuesOneDeathCommandPerDeadUnit()
	{
		using var world = World.Create();
		world.Create(new HealthComponent { Health = 0 });
		world.Create(new HealthComponent { Health = -5 });
		world.Create(new HealthComponent { Health = 3 });

		var bus = new CommandBus();
		new DeathQueueSystem().Operation(new QueueWorld(world, bus));

		Assert.Equal(2, bus.Count<EntityDiedCommand>());
	}

	[Fact]
	public void DeathCommandCarriesTheEntityHandle()
	{
		using var world = World.Create();
		var entity = world.Create(new HealthComponent { Health = 0 });

		var bus = new CommandBus();
		new DeathQueueSystem().Operation(new QueueWorld(world, bus));

		var command = bus.Pending<EntityDiedCommand>()[0];
		Assert.Equal(entity.Id, command.Entity.Id);
		Assert.Equal(entity.Version, command.Entity.Version);
	}

	[Fact]
	public void NothingIsEnqueuedWhenEveryoneIsAlive()
	{
		using var world = World.Create();
		world.Create(new HealthComponent { Health = 1 });

		var bus = new CommandBus();
		new DeathQueueSystem().Operation(new QueueWorld(world, bus));

		Assert.Empty(bus.PendingTypes);
	}
}
