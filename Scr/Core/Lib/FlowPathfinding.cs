using System;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Scr.Core.Resources;
using Godot;
using Godot.Collections;
using static EdgeOfPlain.Scr.Core.Global.Global;

namespace EdgeOfPlain.Scr.Core.Lib;

public class FlowPathfinding
{
	public AStarGrid2D AirAgent = new()
		{ DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)};

	public AStarGrid2D WaterAgent = new()
		{ DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)};

	public AStarGrid2D HoverAgent = new()
		{ DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)};

	public List<AStarGrid2D> LandAgents =
	[
		new() { DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)},
		new() { DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)},
		new() { DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)},
		new() { DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)},
		new() { DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)},
		new() { DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)},
		new() { DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)},
		new() { DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)},
		new() { DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)},
		new() { DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles, CellSize = new Vector2(32, 32) ,Offset = new Vector2(16,16)}
	];
	
	public void GetTileCost(GameTileMap tileMap)
	{
		AirAgent.Region = WaterAgent.Region = HoverAgent.Region =
			LandAgents[0].Region = LandAgents[1].Region = LandAgents[2].Region = LandAgents[3].Region =
			LandAgents[4].Region = LandAgents[5].Region = LandAgents[6].Region =LandAgents[7].Region = 
			LandAgents[8].Region = LandAgents[9].Region =
			new Rect2I(0, 0, (int)tileMap.MapSize.X, (int)tileMap.MapSize.Y);
		
		AirAgent.Update();
		WaterAgent.Update();
		HoverAgent.Update();
		LandAgents[0].Update();
		LandAgents[1].Update();
		LandAgents[2].Update();
		LandAgents[3].Update();
		LandAgents[4].Update();
		LandAgents[5].Update();
		LandAgents[6].Update();
		LandAgents[7].Update();
		LandAgents[8].Update();
		LandAgents[9].Update();
		
		var tiles = tileMap.MapTiles;
		var id = 0;
		foreach (var trueTile in tiles.Select(tile => Instance.Tiles[tile]))
		{
			var pos = new Vector2I((int)(id % tileMap.MapSize.X), (int)(id / tileMap.MapSize.X));
			switch (trueTile.TileMoveType)
			{
				case TileMoveType.None:
					AirAgent.SetPointWeightScale(pos,1);
					WaterAgent.SetPointWeightScale(pos, 1);
					HoverAgent.SetPointWeightScale(pos, 1);
					foreach (var layer in LandAgents)
					{
						layer.SetPointWeightScale(pos, 1);
					}
					break;
				case TileMoveType.Ground:
					AirAgent.SetPointWeightScale(pos,1);
					WaterAgent.SetPointSolid(pos);
					HoverAgent.SetPointWeightScale(pos, 1);
					var i = 0;
					foreach (var layer in LandAgents) 
					{
						if (tileMap.MapHeight[id] > i)
						{
							layer.SetPointSolid(pos);
						}
						else
						{
							layer.SetPointWeightScale(pos, trueTile.Rough);
						}
						i++;
					}
					break;
				case TileMoveType.Water:
					AirAgent.SetPointWeightScale(pos,1);
					WaterAgent.SetPointWeightScale(pos, 1);
					HoverAgent.SetPointWeightScale(pos, 1);
					foreach (var layer in LandAgents)
					{
						layer.SetPointSolid(pos);
					}
					break;
				case TileMoveType.Bridge:
					AirAgent.SetPointWeightScale(pos,1);
					WaterAgent.SetPointWeightScale(pos, 1);
					HoverAgent.SetPointWeightScale(pos, 1);
					foreach (var layer in LandAgents)
					{
						layer.SetPointWeightScale(pos, 1);
					}
					break;
				case TileMoveType.Air:
					AirAgent.SetPointWeightScale(pos,1);
					WaterAgent.SetPointSolid(pos);
					HoverAgent.SetPointSolid(pos);
					foreach (var layer in LandAgents)
					{
						layer.SetPointSolid(pos);
					}
					break;
				case TileMoveType.Void:
					AirAgent.SetPointSolid(pos);
					WaterAgent.SetPointSolid(pos);
					HoverAgent.SetPointSolid(pos);
					foreach (var layer in LandAgents)
					{
						layer.SetPointSolid(pos);
					}
					break;
			}

			id++;
		}
	}

	public Vector2[] GetFlows(string type,Vector2 start, Vector2 sourceTarget)
	{
		var target = new Vector2I((int)sourceTarget.X / 32, (int)sourceTarget.Y / 32);
		var nowAgent = type switch
		{
			"air" => AirAgent,
			"water" => WaterAgent,
			"hover" => HoverAgent,
			"land0" => LandAgents[0],
			"land1" => LandAgents[1],
			"land2" => LandAgents[2],
			"land3" => LandAgents[3],
			"land4" => LandAgents[4],
			"land5" => LandAgents[5],
			"land6" => LandAgents[6],
			"land7" => LandAgents[7],
			"land8" => LandAgents[8],
			"land9" => LandAgents[9],
			_ => throw new Exception()
		};
		var x = nowAgent.Region.Size.X;
		var y = nowAgent.Region.Size.Y;
		var trueStart = new Vector2I((int)start.X / 32, (int)start.Y / 32);
		var trueTarget = new Vector2I(
			target.X < 0 ? 0 : target.X > x ? x : target.X,
			target.Y < 0 ? 0 : target.Y > y ? y : target.Y);
		var path = nowAgent.GetPointPath(trueStart, trueTarget, true);
		path[0] = start;
		path[^1] = sourceTarget;
		path = SimplifiedPath(path);
		return path;
	}

	private static Vector2[] SimplifiedPath(Vector2[] source)
	{
		var result = new List<Vector2>();
		foreach (var point in source.Select((value, index) => new { value, index }))
		{
			if (point.index == 0 || point.index == source.Length - 1)
			{
				result.Add(point.value);
				continue;
			} 
			if (point.value.X.Equals(source[point.index + 1].X) && point.value.X.Equals(source[point.index - 1].X)
			    || point.value.Y.Equals(source[point.index + 1].Y) && point.value.Y.Equals(source[point.index - 1].Y)) continue;
			if ((point.value.X-point.value.Y).Equals(source[point.index+1].X-source[point.index+1].Y) &&
			    (point.value.X-point.value.Y).Equals(source[point.index-1].X-source[point.index-1].Y)) continue;
			result.Add(point.value);
			 
		}
		return result.ToArray();
	}
}