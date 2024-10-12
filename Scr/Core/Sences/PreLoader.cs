using Godot;
using System;
using EdgeOfPlain.Scr.Core;
using static EdgeOfPlain.Scr.Core.Global.Global;
using EdgeOfPlain.Scr.Core.Resources;

namespace EdgeOfPlain.Scr.Core.Sences;

public partial class PreLoader : Control
{
	public override void _Ready()
	{
		Launcher.LoadTiles();
		Launcher.LoadUnits();
		Instance.GameMapPath = "res://Res/Maps/TestMap.tilemap";
		//GetTree().ChangeSceneToFile("res://Sen/MapEditor.tscn");
		GetTree().ChangeSceneToFile("res://Sen/Game.tscn");
	}
}
