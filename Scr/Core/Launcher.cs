using System;
using EdgeOfPlain.Scr.Core.Resources;
using Godot;
using static EdgeOfPlain.Scr.Core.Global.Global;
namespace EdgeOfPlain.Scr.Core;

public static class Launcher
{
	public static void Launch(string args="")
	{
		LoadTiles();
		LoadUnits();
	}

	public static void LoadTiles(string path = "res://Res/")
	//grass stone muddy water ice waterBridge lava void
	{
		Tile.NewTile(new GroundTile("Grass"));
		Tile.NewTile(new GroundTile("Stone"){Humidity = 3f});
		Tile.NewTile(new GroundTile("Mud"){Rough = 1.5f,Humidity = 7f});
		Tile.NewTile(new WaterTile("Water"));
		Tile.NewTile(new GroundTile("Ice"){Rough = 0.5f,Temperature = 2f});
		Tile.NewTile(new WaterTile("WaterBridge"){TileMoveType = TileMoveType.Bridge});
		Tile.NewTile(new Tile("Lava")
		{
			TileMoveType = TileMoveType.Air,Temperature = 10f,
			CanLighted = true,LightColor = Color.Color8(255,149,49)
		});
		Tile.NewTile(new Tile("Void"){TileMoveType = TileMoveType.Void});
		Tile.NewTile(new GroundTile("FluorescentGrass")
			{CanLighted = true,LightColor = Color.Color8(0,255,255)});
	}

	public static void LoadUnits(string path = "res://Res/")
	{
		//NNSA
		Unit.NewUnit(new Unit("NNSA/BaseInfantry")
		{
			Radius = 8,
			UnitMoveType = UnitMoveType.Ground,
			TotalFrames = 4,
			//Mass = 50f
		});

	}
}