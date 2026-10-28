using Arch.Core;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Game.Systems;

namespace EdgeOfPlain.Core.Content.Systems.Queues;

public class DeathQueueSystem : IQueueSystem
{
	public void Operation(QueueWorld world)
	{
		world.Query(new QueryDescription(), (in EntityHandle entity, in HealthComponent health) =>
		{
			if (health.Health <= 0) world.Enqueue(new EntityDiedCommand(entity));
		});
	}
}
