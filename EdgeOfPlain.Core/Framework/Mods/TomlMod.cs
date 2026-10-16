using System;

namespace EdgeOfPlain.Core.Framework.Mods;

public sealed class TomlMod(ModPackage package) : IMod
{
	public ModPackage Package { get; } = package;

	public Type[]? RegisterTypes => null;

	public void InitMod(ModContext context)
	{
		
	}
}
