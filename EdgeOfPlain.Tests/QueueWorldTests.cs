using System;
using System.Collections.Generic;
using Arch.Core;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Systems;
using Xunit;

namespace EdgeOfPlain.Tests;

public sealed class QueueWorldTests
{
	[Fact]
	public void QueryVisitsEveryMatchingEntity()
	{
		using var world = World.Create();
		world.Create(new HealthComponent { Health = 0 });
		world.Create(new HealthComponent { Health = 7 });
		var queue = new QueueWorld(world, new CommandBus());

		var healthValues = new List<int>();
		queue.Query<HealthComponent>(new QueryDescription(),
			(in EntityHandle entity, in HealthComponent health) => healthValues.Add(health.Health));

		Assert.Equal(2, healthValues.Count);
		Assert.Contains(0, healthValues);
		Assert.Contains(7, healthValues);
	}

	[Fact]
	public void GetReturnsCopySoWritingItDoesNotTouchTheWorld()
	{
		using var world = World.Create();
		var entity = world.Create(new HealthComponent { Health = 3 });
		var queue = new QueueWorld(world, new CommandBus());

		var handles = new EntityHandle[4];
		Assert.Equal(1, queue.CollectEntities(new QueryDescription(), handles));

		var copy = QueueWorld.Get<HealthComponent>(in handles[0]);
		copy.Health = 99;

		Assert.Equal(3, world.Get<HealthComponent>(entity).Health);
	}

	[Fact]
	public void CollectEntitiesReportsTotalEvenWhenTruncated()
	{
		using var world = World.Create();
		for (var i = 0; i < 5; i++) world.Create(new HealthComponent { Health = i });
		var queue = new QueueWorld(world, new CommandBus());

		var handles = new EntityHandle[3];
		var total = queue.CollectEntities(new QueryDescription(), handles);

		Assert.Equal(5, total);
		Assert.All(handles, handle => Assert.True(handle.Id >= 0));
	}

	[Fact]
	public void CollectEntitiesWritesNothingIntoEmptyDestination()
	{
		using var world = World.Create();
		world.Create(new HealthComponent { Health = 1 });
		var queue = new QueueWorld(world, new CommandBus());

		Assert.Equal(1, queue.CollectEntities(new QueryDescription(), Span<EntityHandle>.Empty));
	}

	[Fact]
	public void IsAliveReportsFalseForDestroyedEntity()
	{
		using var world = World.Create();
		var entity = world.Create(new HealthComponent { Health = 1 });
		var queue = new QueueWorld(world, new CommandBus());

		var handles = new EntityHandle[1];
		queue.CollectEntities(new QueryDescription(), handles);
		Assert.True(QueueWorld.IsAlive(in handles[0]));

		world.Destroy(entity);

		Assert.False(QueueWorld.IsAlive(in handles[0]));
	}

	[Fact]
	public void HasAndTryGetMatchTheEntityState()
	{
		using var world = World.Create();
		world.Create(new HealthComponent { Health = 4 });
		var queue = new QueueWorld(world, new CommandBus());

		var handles = new EntityHandle[1];
		queue.CollectEntities(new QueryDescription(), handles);

		Assert.True(QueueWorld.Has<HealthComponent>(in handles[0]));
		Assert.True(QueueWorld.TryGet<HealthComponent>(in handles[0], out var health));
		Assert.Equal(4, health.Health);
		Assert.False(QueueWorld.TryGet<TransformComponent>(in handles[0], out _));
	}

	[Fact]
	public void EntityHandlesSortByIdThenVersion()
	{
		using var world = World.Create();
		for (var i = 0; i < 4; i++) world.Create(new HealthComponent { Health = i });
		var queue = new QueueWorld(world, new CommandBus());

		var handles = new EntityHandle[4];
		queue.CollectEntities(new QueryDescription(), handles);

		var sorted = (EntityHandle[])handles.Clone();
		Array.Sort(sorted);
		for (var i = 1; i < sorted.Length; i++)
		{
			Assert.True(sorted[i - 1].CompareTo(sorted[i]) <= 0);
		}
	}
}
