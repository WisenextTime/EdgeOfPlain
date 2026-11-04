using Arch.Core;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Systems;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Content.Systems.Frames;

public class EntityDeathQueueSystem : IQueueSystem
{
	public void Operation(QueueWorld world)
	{
		var description = new QueryDescription().WithAll<HealthComponent>();
		world.Query<HealthComponent>(description, (in EntityHandle handle, in HealthComponent health) =>
		{
			if (health.Health <= 0) world.Enqueue(new DeleteEntityCommand(handle));
		});
	}
}