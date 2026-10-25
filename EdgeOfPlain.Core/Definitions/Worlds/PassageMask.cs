using System;

namespace EdgeOfPlain.Core.Definitions.Worlds;

[Flags]
public enum PassageMask
{
	None = 0,
	Ground = 1 << 0,
	Water = 1 << 1,
	Air = 1 << 2,
	Cliff = 1 << 3,
}