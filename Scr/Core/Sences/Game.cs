using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdgeOfPlain.Scr.Core.Resources;
using Godot;
using Microsoft.VisualBasic;
using static EdgeOfPlain.Scr.Core.Global.Global;
using Array = Godot.Collections.Array;
using Color = System.Drawing.Color;

namespace EdgeOfPlain.Scr.Core.Sences;

public partial class Game : Control
{
    //[Signal] public delegate void UnselectedEventHandler(int team);
    //[Signal] public delegate void SelectedEventHandler(Rect2 range, int team);

    public bool MouseLeftPressed;
    public bool MouseRightPressed;
    public bool MouseMiddlePressed;
    public Navigation Navigation;
    public int TeamId;
    public int TeamGroup;
    private readonly Dictionary<string, int> _tileIndex = [];
    public GameTileMap TileMap;
    public Camera2D GameCamera;
    public TileMapLayer Tiles;
    public TileMapLayer Height;
    private Line2D _selectLine;
    private float _cameraZoom;
    private Vector2 startPos;
    private Window _window;

    private Vector2 _lastMousePos;

    public override void _Ready()
    {
        _window = GetTree().Root;
        TileMap = MapParser.Load(Instance.GameMapPath);
        Navigation = GetNode<Navigation>("Navigation");
        Tiles = GetNode<TileMapLayer>("Tiles");
        Height = GetNode<TileMapLayer>("Height");
        _selectLine = GetNode<Line2D>("SelectBar");
        GameCamera = GetNode<Camera2D>("Camera");
        GameCamera.GlobalPosition = new Vector2(TileMap.MapSize.X * 16, TileMap.MapSize.Y * 16);

        InitTileIndexDictionary();
        DrawMap();
        DebugUnits(100, 100);
    }

    private void DebugUnits(int a, int b)
    {
        var unitPrefab = ResourceLoader.Load<PackedScene>("res://Sen/Unit.tscn");
        for (var _ = 0; _ < a; _++)
        {
            var nowUnit = unitPrefab.Instantiate<GameUnit>();
            nowUnit.TeamId = 0;
            nowUnit.TeamGroup = 0;
            Vector2 targetPos;
            do
            {
                targetPos = new Vector2(new Random().Next(1000, 1200), new Random().Next(1300, 1900));
            } while (GetNode("Units").GetChildren().Cast<GameUnit>().Any(c => (c.GlobalPosition - targetPos).LengthSquared() < Mathf.Pow(Math.Max(nowUnit.UnitData.Radius, c.UnitData.Radius), 3)));
            nowUnit.GlobalPosition = targetPos;
            GetNode("Units").AddChild(nowUnit);
        }

        for (var _ = 0; _ < b; _++)
        {
            var nowUnit = unitPrefab.Instantiate<GameUnit>();
            nowUnit.TeamId = 1;
            nowUnit.TeamGroup = 1;
            nowUnit.GlobalPosition = new Vector2(new Random().Next(2200, 2400), new Random().Next(1300, 1900));
            GetNode("Units").AddChild(nowUnit);
        }
    }

    private void InitTileIndexDictionary()
    {
        _tileIndex.Clear();
        var index = 0;
        foreach (var tile in Instance.Tiles)
        {
            _tileIndex.Add(tile.Value.Id, index++);
        }
    }

    private void DrawMap()
    {
        // 扩展的 Tile Source 从 100 开始编号，前 100 作为保留地形供自己使用。
        // TODO 将不同地形的 Source 直接在 Tile.NewTile() 时，作为该图块的只读属性初始化，后续通过获取属性的放置直接调用
        foreach (var tile in TileMap.MapTiles.Select((tile, index) => (tile, index)))
        {
            var pos = new Vector2I((int)(tile.index % TileMap.MapSize.X), (int)(tile.index / TileMap.MapSize.X));
            Tiles.SetCell(pos, _tileIndex[tile.tile], Vector2I.Zero);
            Height.SetCell(pos, 0, new Vector2I(0, TileMap.MapHeight[tile.index]));
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton button:
                MouseButtonInput(button);
                break;
            case InputEventMouseMotion motion:
                if (MouseRightPressed)
                {
                    GameCamera.GlobalPosition -= motion.Relative / GameCamera.Zoom.X;
                }
                break;
        }
    }

    private void MouseButtonInput(InputEventMouseButton @event)
    {
        switch (@event.ButtonIndex)
        {
            case MouseButton.Left:
                MouseLeftPressed = @event.Pressed;
                if (@event.Pressed)
                {
                    startPos = GetGlobalMousePosition();
                }
                else
                {
                    // Hack 逻辑有待优化
                    if (!Input.IsKeyPressed(Key.Shift))
                    {
                        GetTree().CallGroup("Unit", GameUnit.MethodName.OnDeselected, GetMouseRect());
                    }
                    var unit = (GameUnit)GetTree().GetFirstNodeInGroup("Selected");
                    if (unit != null)
                    {
                        unit.Selected = true;
                    }
                    GetTree().CallGroup("Unit", GameUnit.MethodName.OnSelected, GetMouseRect());
                    _selectLine.ClearPoints();
                }
                break;
            case MouseButton.Right:
                MouseRightPressed = @event.Pressed;

                if (@event.Pressed)
                {
                    _lastMousePos = GetTree().Root.GetMousePosition();
                }
                else if (GetTree().Root.GetMousePosition().DistanceSquaredTo(_lastMousePos) < 64f)
                {
                    var unit = (GameUnit)GetTree().GetFirstNodeInGroup("Selected");
                    if (unit == null)
                    {
                        GetTree().CallGroup("Unit", GameUnit.MethodName.NewWayPoint, "Move",
                            new Array { GetGlobalMousePosition() });
                    }
                    else
                    {
                        if (unit.TeamGroup == TeamGroup) return;
                        GetTree().CallGroup("Unit", GameUnit.MethodName.NewWayPoint, "Attack",
                            new Array { unit });
                    }
                }
                break;
            case MouseButton.Middle:
                MouseMiddlePressed = @event.Pressed;
                break;
            case MouseButton.WheelUp:
                _cameraZoom = Mathf.Min(_cameraZoom + 0.2f, 3);
                break;
            case MouseButton.WheelDown:
                _cameraZoom = Mathf.Max(_cameraZoom - 0.2f, -2);
                break;
        }
    }

    public override void _Process(double delta)
    {
        _selectLine.Width = 5 / GameCamera.Zoom.X;
        _selectLine.ClearPoints();
        GameCamera.Zoom = Vector2.One * Mathf.Pow(2, _cameraZoom);
        if (MouseLeftPressed)
        {
            _selectLine.AddPoint(startPos);
            _selectLine.AddPoint(new Vector2(startPos.X, GetGlobalMousePosition().Y));
            _selectLine.AddPoint(GetGlobalMousePosition());
            _selectLine.AddPoint(new Vector2(GetGlobalMousePosition().X, startPos.Y));
        }

        if (_window.GetMousePosition().X < 10 && GameCamera.GlobalPosition.X >= 0)
            GameCamera.GlobalPosition += Vector2.Left * 10 / GameCamera.Zoom.X;
        if (_window.GetMousePosition().X > _window.Size.X - 10 && GameCamera.GlobalPosition.X <= TileMap.MapSize.X * 32)
            GameCamera.GlobalPosition += Vector2.Right * 10 / GameCamera.Zoom.X;
        if (_window.GetMousePosition().Y < 10 && GameCamera.GlobalPosition.Y >= 0)
            GameCamera.GlobalPosition += Vector2.Up * 10 / GameCamera.Zoom.X;
        if (_window.GetMousePosition().Y > _window.Size.Y - 10 && GameCamera.GlobalPosition.Y <= TileMap.MapSize.Y * 32)
            GameCamera.GlobalPosition += Vector2.Down * 10 / GameCamera.Zoom.X;
    }


    private Rect2 GetMouseRect() => GetRectByTwoPoints(startPos, GetGlobalMousePosition());

    private static Rect2 GetRectByTwoPoints(Vector2 a, Vector2 b)
    {
        var (minX, maxX) = a.X < b.X ? (a.X, b.X) : (b.X, a.X);
        var (minY, maxY) = a.Y < b.Y ? (a.Y, b.Y) : (b.Y, a.Y);
        var from = new Vector2(minX, minY);
        var to = new Vector2(maxX, maxY);
        return new Rect2(from, to - from);
    }
}