using System;

namespace EdgeOfPlain.Core.Framework.Mods;

public interface IMod<T> where T : IMod<T>, new()
{
	ModInfo ModInfo { get; }
	Type[]? RegisterTypes { get; }
	void InitMod(ModContext context);
}