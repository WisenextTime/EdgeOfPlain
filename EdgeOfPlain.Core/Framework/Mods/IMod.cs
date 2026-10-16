using System;

namespace EdgeOfPlain.Core.Framework.Mods;

public interface IMod
{
	public ModInfo ModInfo => ModManager.GetModInfo(this);
	Type[]? RegisterTypes { get; }
	void InitMod(ModContext context);
}
