using System;

namespace EdgeOfPlain.Core.Framework.Mods;

public class Mod
{
	public virtual ModInfo ModInfo => ModManager.GetModInfo(this);
	public virtual Type[]? RegisterTypes { get; protected set; } = null;

	public virtual void InitMod(ModContext context) { }
	public virtual void Preload() { }
}