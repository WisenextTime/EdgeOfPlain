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
	private Dictionary<string, int> _tileIndex = [];
	public GameTileMap TileMap;
	public Camera2D GameCamera;
	public TileMapLayer Tiles;
	public TileMapLayer Height;
	private Line2D _selectLine;
	private float _cameraZoom;
	private Vector2 startPos;
	private Label _fpsLabel;

	private Vector2 _lastMousePos;
	
	public override void _Ready()
	{
		TileMap = MapParser.Load(Instance.GameMapPath);
		Navigation = GetNode<Navigation>("Navigation");
		Tiles = GetNode<TileMapLayer>("Tiles");
		Height = GetNode<TileMapLayer>("Height");
		_selectLine = GetNode<Line2D>("SelectBar");
		_fpsLabel = GetNode<Label>("UI/FPS");
		GameCamera = GetNode<Camera2D>("Camera");
		GameCamera.GlobalPosition = new Vector2(TileMap.MapSize.X * 16, TileMap.MapSize.Y * 16);
		
		GetTiles();
		DrawMap();
		DebugUnits();
	}

	private void DebugUnits()
	{
		for (var _ = 0; _ <= 100; _++)
		{
			var unit = (PackedScene)ResourceLoader.Load<PackedScene>("res://Sen/Unit.tscn").Duplicate();
			var nowUnit = (GameUnit)unit.Instantiate();
			nowUnit.TeamId = 0;
			nowUnit.TeamGroup = 0;
			nowUnit.GlobalPosition = new Vector2(new Random().Next(1000, 1200), new Random().Next(1300, 1900));
			GetNode("Units").AddChild(nowUnit);
		}
	}

	private void GetTiles()
	{
		var tileSet = new TileSet();
		Tiles.TileSet = tileSet;
		tileSet.TileSize = Vector2I.One * 32;
		//0000_00
		//HASL
		//Ground
		tileSet.AddPhysicsLayer(0);
		tileSet.SetPhysicsLayerCollisionLayer(0,0b_0010_00);
		//Water
		tileSet.AddPhysicsLayer(1);
		tileSet.SetPhysicsLayerCollisionLayer(1,0b_0001_00);
		//Bridge
		tileSet.AddPhysicsLayer(2);
		tileSet.SetPhysicsLayerCollisionLayer(2,0b_0000_00);
		//Air
		tileSet.AddPhysicsLayer(3);
		tileSet.SetPhysicsLayerCollisionLayer(3,0b_1011_00);
		//Void
		tileSet.AddPhysicsLayer(4);
		tileSet.SetPhysicsLayerCollisionLayer(4,0b_1111_00);
		
		tileSet.AddCustomDataLayer(0);
		tileSet.SetCustomDataLayerName(0,"Rough");
		tileSet.SetCustomDataLayerType(0,Variant.Type.Float);
		
		foreach (var tile in Instance.Tiles.Select((tile, index) => (tile.Value, index)))
		{
			_tileIndex.Add(tile.Value.Id, tile.index);
			var source = new TileSetAtlasSource();
			tileSet.AddSource(source, tile.index);
			source.Texture = tile.Value.Texture;
			source.TextureRegionSize = Vector2I.One * 32;
			source.CreateTile(Vector2I.Zero);
			var nowTile = source.GetTileData(Vector2I.Zero, 0);
			nowTile.SetCustomData("Rough",tile.Value.Rough);
			var layer = tile.Value.TileMoveType switch
			{
				TileMoveType.Ground => 0,
				TileMoveType.Water => 1,
				TileMoveType.Bridge => 2,
				TileMoveType.Air => 3,
				TileMoveType.Void => 4,
				_ =>-1
			};
			Vector2[] polygon = [new(-16, -16), new(-16, 16), new(16, 16), new(16, -16)];
			if (layer == -1) continue;
			nowTile.AddCollisionPolygon(layer);
			nowTile.SetCollisionPolygonPoints(layer,0,polygon);
			if (!tile.Value.CanLighted) continue;
			nowTile.Material = (ShaderMaterial)ResourceLoader.Load("res://Res/Shaders/LightMateral.tres").Duplicate();
			var shader = (ShaderMaterial)nowTile.Material;
			shader.SetShaderParameter("LightColor",tile.Value.LightColor);
			shader.SetShaderParameter("LightTexture",tile.Value.LightTexture);
		}
		ResourceSaver.Save(tileSet, "res://tiles.tres");
	}

	private void DrawMap()
	{
		foreach (var tile in TileMap.MapTiles.Select((tile, index) => (tile, index)))
		{
			var pos = new Vector2I((int)(tile.index%TileMap.MapSize.X),(int)(tile.index/TileMap.MapSize.X));
			Tiles.SetCell(pos, _tileIndex[tile.tile],Vector2I.Zero);
			Height.SetCell(pos,0, new Vector2I(0,TileMap.MapHeight[tile.index]));
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton button:
			{
				switch (button.ButtonIndex)
				{
					case MouseButton.Left:
						MouseLeftPressed = button.Pressed;
						if (button.Pressed)
						{
							startPos = GetGlobalMousePosition();
						}
						else
						{
							if (!Input.IsKeyPressed(Key.Shift))
							{
								GetTree().CallGroup("Unit", GameUnit.MethodName.OnDeselected,
									new Rect2(startPos, GetGlobalMousePosition() - startPos));
							}

							GetTree().CallGroup("Unit", GameUnit.MethodName.OnSelected,
								new Rect2(startPos, GetGlobalMousePosition() - startPos));
							_selectLine.ClearPoints();
						}
						break;
					case MouseButton.Right:
						MouseRightPressed = button.Pressed;

						if (button.Pressed)
						{
							_lastMousePos = GetTree().Root.GetMousePosition();
						}
						else if(GetTree().Root.GetMousePosition().DistanceSquaredTo(_lastMousePos) < 64f)
						{
							GetTree().CallGroup("Unit", GameUnit.MethodName.NewWayPoint, "move",
								new Array { GetGlobalMousePosition() });
						}
						break;
					case MouseButton.Middle:
						MouseMiddlePressed = button.Pressed;
						break;
					case MouseButton.WheelUp:
						_cameraZoom = Mathf.Min(_cameraZoom + 0.2f, 3);
						break;
					case MouseButton.WheelDown:
						_cameraZoom = Mathf.Max(_cameraZoom - 0.2f, -2);
						break;
				}
				
				break;
			}
			case InputEventMouseMotion motion:
				if (MouseRightPressed)
				{
					GameCamera.GlobalPosition -= motion.Relative / GameCamera.Zoom.X;
				}
				break;
		}
	}

	public override void _Process(double delta)
	{
		_fpsLabel.Text = $"FPS :  {(int)Engine.GetFramesPerSecond()}";
		_fpsLabel.Modulate = Engine.GetFramesPerSecond() > 60? Colors.Green :
			Engine.GetFramesPerSecond() > 30? Colors.Yellow : Colors.Red;
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
	}
}