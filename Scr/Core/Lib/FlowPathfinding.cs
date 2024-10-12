using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using EdgeOfPlain.Scr.Core.Resources;
using Godot;
using Godot.Collections;
using Microsoft.Win32;
using static EdgeOfPlain.Scr.Core.Global.Global;

namespace EdgeOfPlain.Scr.Core.Lib;

public class FlowPathfinding
{
	public AStarGrid2D LandLayer = new() {DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles};
	public AStarGrid2D AirLayer = new() {DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles};
	public AStarGrid2D SeaLayer = new() {DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles};
	public AStarGrid2D HoverLayer = new() {DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles};

	public FlowPathfinding(Vector2I size,GameTileMap tileMap)
	{
		LandLayer.Region = AirLayer.Region = SeaLayer.Region = HoverLayer.Region = new Rect2I(Vector2I.Zero, size);
		LandLayer.CellSize = AirLayer.CellSize = SeaLayer.CellSize = HoverLayer.CellSize = Vector2.One * 32;
		LandLayer.Offset = AirLayer.Offset = SeaLayer.Offset = HoverLayer.Offset = Vector2.One * 16;
		LandLayer.Update();
		AirLayer.Update();
		SeaLayer.Update();
		HoverLayer.Update();
		LandLayer.FillWeightScaleRegion(LandLayer.Region, 1);
		AirLayer.FillWeightScaleRegion(AirLayer.Region, 1);
		SeaLayer.FillWeightScaleRegion(SeaLayer.Region, 1);
		HoverLayer.FillWeightScaleRegion(HoverLayer.Region, 1);
		foreach (var tile in tileMap.MapTiles.Select((tile, index) => new { tile, index }))
		{
			var pos = new Vector2I((int)(tile.index%tileMap.MapSize.X), (int)(tile.index/tileMap.MapSize.X));
			switch (Instance.Tiles[tile.tile].TileMoveType)
			{
				case TileMoveType.Ground:
				{
					SeaLayer.SetPointSolid(pos);
					LandLayer.SetPointWeightScale(pos,Instance.Tiles[tile.tile].Rough);
					break;
				}
				case TileMoveType.Air:
				{
					SeaLayer.SetPointSolid(pos);
					LandLayer.SetPointSolid(pos);
					HoverLayer.SetPointSolid(pos);
					break;
				}
				case TileMoveType.Water:
				{
					LandLayer.SetPointSolid(pos);
					break;
				}
				case TileMoveType.Bridge:
				{
					break;
				}
				case TileMoveType.Void:
				{
					SeaLayer.SetPointSolid(pos);
					LandLayer.SetPointSolid(pos);
					HoverLayer.SetPointSolid(pos);
					AirLayer.SetPointSolid(pos);
					break;
				}
			}
		}
	}

	public Vector2[] GetPath(Vector2 fromPos, Vector2 toPos, string type)
	{
		var nowNavigation = type switch
		{
			"Air" => AirLayer,
			"Water" => SeaLayer,
			"Hover" => HoverLayer,
			_ => LandLayer,
		};
		var origin = nowNavigation.IsPointSolid(new Vector2I((int)(toPos.X / 32), (int)(toPos.Y / 32)));
		nowNavigation.SetPointSolid(new Vector2I((int)(toPos.X/32), (int)(toPos.Y/32)),false);
		var path = nowNavigation.GetPointPath
			(new Vector2I((int)(fromPos.X/32), (int)(fromPos.Y/32)), new Vector2I((int)(toPos.X/32), (int)(toPos.Y/32)), true);
		nowNavigation.SetPointSolid(new Vector2I((int)(toPos.X/32), (int)(toPos.Y/32)),origin);
		path[0] = fromPos;
		path[^1] = toPos;
		return path;
	}
}