using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Godot;
using EdgeOfPlain.Scr.Core.Resources;
using EdgeOfPlain.Scr.Core;
using FileAccess = System.IO.FileAccess;
using Vector2 = System.Numerics.Vector2;

namespace EdgeOfPlain.Scr.Debug;

public partial class NewDebugMap : Node
{
	public override void _Ready()
	{
		Launcher.Launch();
		var newMap = MapParser.NewTileMap("TestMap", "WisenextTime", new Vector2(100, 100));
		MapParser.Save(newMap, "C://StarsSailing/EdgeOfPlain/maps/TestMap.tilemap");
		newMap = MapParser.NewTileMap("TestMap", "WisenextTime", new Vector2(1000, 1000));
		MapParser.Save(newMap, "C://StarsSailing/EdgeOfPlain/maps/TestMapLarge.tilemap");
		GetTree().Quit();
	}
}