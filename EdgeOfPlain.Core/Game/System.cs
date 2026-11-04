using Arch.Core;
using FixedMathSharp;

namespace EdgeOfPlain.Core.Game;

public interface ISystem
{
	void Operation(GameWorld world, Fixed64 delta);
}