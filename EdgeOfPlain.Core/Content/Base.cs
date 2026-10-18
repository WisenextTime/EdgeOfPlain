using System;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Mods;

namespace EdgeOfPlain.Core.Content;

public class Base : Mod
{
	public override ModInfo ModInfo { get; } = new("base", new Version(1, 0, 0), "WisenextTime", "The base pack of game.");

	public override Type[]? RegisterTypes { get; protected set; } = [
		typeof(TerrainDef),
		typeof(TilesetDef), 
		typeof(TileRendererDef)];
	
	public override void InitMod(ModContext context)
	{
		Terrains.RegisterAll(context);
	}
}
