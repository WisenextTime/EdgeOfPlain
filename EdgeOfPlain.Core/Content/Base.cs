using System;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;

namespace EdgeOfPlain.Core.Content;

public class Base : IMod<Base>
{

	public ModInfo ModInfo { get; } = new("base", new Version(1, 0, 0));

	public Type[]? RegisterTypes { get; } = [typeof(TerrainDef)];

	public void InitMod(ModContext context)
	{
		
	}
}