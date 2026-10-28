using Arch.Core;

namespace EdgeOfPlain.Core.Game.Systems;

public interface IFrameSystem
{
	void Operation(World world, float delta);
}
