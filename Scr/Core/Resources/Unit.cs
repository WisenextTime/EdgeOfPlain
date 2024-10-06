using System;
using System.Collections.Generic;
using EdgeOfPlain.Scr.Core.Lib;
using Godot;
using static EdgeOfPlain.Scr.Core.Global.Global;

namespace EdgeOfPlain.Scr.Core.Resources;

public class Unit(string id)
{
	public string Id {get;protected init;} = id;
	public int Radius { get; set; } = 0;
	
	//Images
	public string ImageName { get; set; } = IndexImage.Default;
	public SpriteFrames Texture => ImageLoader.LoadAnimatedImage(ImageName, "unit", Id);
	public int TotalFrames { get; set; } = 1;
	
	//Movement
	//public float Mass = 1;
	public UnitMoveType UnitMoveType { get; set; } = UnitMoveType.None;
	public int AccessableHeight { get; set; } = 0;
	public float MoveSpeed { get; set; } = 1f;
	public float RotationSpeed { get; set; } = 1f;
	public float TargetHeight { get; set; } = 0;
	

	public static void NewUnit(Unit unit)
	{
		Instance.Units.Add(unit.Id,unit);
	}
}