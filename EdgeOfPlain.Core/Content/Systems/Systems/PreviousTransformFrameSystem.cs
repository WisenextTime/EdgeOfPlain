using Arch.Core;
using EdgeOfPlain.Core.Content.Components;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Systems;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Content.Systems.Frames;

public class PreviousTransformFrameSystem : IFrameSystem
{
	public void Operation(World world, Fixed64 delta)
	{
		var description = new QueryDescription().WithAll<TransformComponent>();
		world.Query(description, (ref TransformComponent transform) =>
		{
			transform.PreviousPosition = transform.Position;
			transform.PreviousRotation = transform.Rotation;
		});
	}
}