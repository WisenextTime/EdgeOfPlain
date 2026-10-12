using System;

namespace EdgeOfPlain.Core.Framework.Mods;

public interface IMod
{
	ModInfo ModInfo { get; }
	Type[]? RegisterTypes { get; }
	void InitMod(ModContext context);
}
