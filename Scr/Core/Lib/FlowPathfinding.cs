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
    public AStarGrid2D LandLayer = new();
    public AStarGrid2D AirLayer = new();
    public AStarGrid2D SeaLayer = new();
    public AStarGrid2D HoverLayer = new();

    private Rect2I Size;

    public FlowPathfinding(Vector2I size, GameTileMap tileMap)
    {
        void Init(AStarGrid2D a)
        {
            a.Region = new(Vector2I.Zero, size);
            a.CellSize = Vector2.One * 32;
            a.Update();
            a.FillWeightScaleRegion(a.Region, 1);
            a.Offset = 16 * Vector2I.One;
            a.DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles;
            a.Update();
        }

        Size = new(Vector2I.Zero, size);

        Init(LandLayer);
        Init(AirLayer);
        Init(SeaLayer);
        Init(HoverLayer);

        foreach (var tile in tileMap.MapTiles.Select((tile, index) => new { tile, index }))
        {
            var pos = new Vector2I((int)(tile.index % tileMap.MapSize.X), (int)(tile.index / tileMap.MapSize.X));
            switch (Instance.Tiles[tile.tile].TileMoveType)
            {
                case TileMoveType.Ground:
                    {
                        SeaLayer.SetPointSolid(pos);
                        LandLayer.SetPointWeightScale(pos, Instance.Tiles[tile.tile].Rough);
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
        Vector2I fromCoords = (Vector2I)(fromPos / 32), toCoords = (Vector2I)(toPos / 32);
        if (!Size.HasPoint(fromCoords) || !Size.HasPoint(toCoords)) return null;
        var origin = nowNavigation.IsPointSolid(toCoords);
        nowNavigation.SetPointSolid(toCoords, false);
        var path = nowNavigation.GetPointPath(fromCoords, toCoords, false);
        nowNavigation.SetPointSolid(toCoords, origin);
        //path = path.Skip(1).ToArray();
        path[0] = fromPos;
        path[^1] = toPos;
        return path;
    }
}