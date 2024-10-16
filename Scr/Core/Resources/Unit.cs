﻿using System;
using System.Collections.Generic;
using EdgeOfPlain.Scr.Core.Lib;
using Godot;
using static EdgeOfPlain.Scr.Core.Global.Global;

namespace EdgeOfPlain.Scr.Core.Resources;

public class Unit(string id)
{
	public string Id {get; } = id;
	public int Radius { get; set; } = 0;
	
	public float SightRadius { get; set; } = 10;
	
	//Images
	public string ImageName { get; set; } = IndexImage.Default;
	public SpriteFrames Texture => ImageLoader.LoadAnimatedImage(ImageName, "Unit", Id);
	public int TotalFrames { get; set; } = 1;
	
	//Movement
	//public float Mass = 1;
	public UnitMoveType UnitMoveType { get; set; }
	public int AccessableHeight { get; set; } = 0;
	public float MoveSpeed { get; set; } = 1f;
	public float RotationSpeed { get; set; } = 2f;
	public float TargetHeight { get; set; } = 0;
	
	//Attack
	public float AttackRange { get; set; } = 0;
	

	public static void NewUnit(Unit unit)
	{
		Instance.Units.Add(unit.Id,unit);
	}
}