using Arch.Core;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Game.Systems;

public interface IFrameSystem
{
	void Operation(World world, Fixed64 delta);
}
