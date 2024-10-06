using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using static EdgeOfPlain.Scr.Core.Global.Global;
using  EdgeOfPlain.Scr.Core.Resources;

namespace EdgeOfPlain.Scr.Core;

public partial class MapEditor : Control
{
	
	private TileMapLayer _tiles;
	private TileMapLayer _height;
	private Camera2D _camera;
	private OptionButton _selectButton;
	private HSlider _zoom;
	private ItemList _pens;
	private ColorRect _background;
	private Panel _menu;
	
	private Dictionary<string, int> _tileIndex = [];
	private GameTileMap _mapData;
	
	private Vector2I _mousePosition;

	private bool _isPressed;
	private List<int> _operatedTiles = [];
	private int _lastSelected;
	
	public override void _Ready()
	{
		_mapData = MapParser.Load(Instance.GameMapPath);
		_camera = GetNode<Camera2D>("Camera");
		_camera.GlobalPosition = new Vector2(_mapData.MapSize.X * 16, _mapData.MapSize.Y * 16);
		_tiles = GetNode<TileMapLayer>("Tiles");
		_height = GetNode<TileMapLayer>("Height");
		_zoom = GetNode<HSlider>("UI/ToolBox/Zoom");
		_pens = GetNode<ItemList>("UI/ToolBox/Pens");
		_pens.Select(0);
		_selectButton = GetNode<OptionButton>("UI/ToolBox/TileSelect");
		_background = GetNode<ColorRect>("UI/Background");
		_menu = GetNode<Panel>("UI/Menu");
		GetTiles();
		try
		{
			DrawMap();
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			throw;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		var nowMousePosition = GetGlobalMousePosition();
		_mousePosition = (int)(nowMousePosition.X/32.0) < 0 || (int)(nowMousePosition.X/32.0) >= _mapData.MapSize.X ||
			(int)(nowMousePosition.Y/32.0) < 0 || (int)(nowMousePosition.Y/32.0) >= _mapData.MapSize.Y ?
			new Vector2I(-1,-1) :
			new Vector2I((int)(nowMousePosition.X/32.0),(int)(nowMousePosition.Y/32.0));
		var nowId = (int)(_mousePosition.X + _mousePosition.Y*_mapData.MapSize.X);
		
		GetNode<Label>("UI/Bar/Info").Text = 
			_mousePosition != new Vector2I(-1,-1) ?
			$"{Tr($"Tile_{_mapData.MapTiles[nowId]}_Name")}" +
			$" ( {_mousePosition.X} , {_mousePosition.Y} )" +
			$"     {_mapData.MapHeight[nowId]}"
			: "";
		//Console.WriteLine($"Mouse Position: {_mousePosition}");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventKey eventKey:
			{
				var cameraX = _camera.GlobalPosition.X;
				var cameraY = _camera.GlobalPosition.Y;
				_camera.GlobalPosition = eventKey.Keycode switch
				{
					Key.A => new Vector2(cameraX - 10, cameraY),
					Key.D => new Vector2(cameraX + 10, cameraY),
					Key.W => new Vector2(cameraX, cameraY - 10),
					Key.S => new Vector2(cameraX, cameraY + 10),
					Key.H => new Vector2(_mapData.MapSize.X * 16, _mapData.MapSize.Y * 16),
					_ => _camera.GlobalPosition
				};
				break;
			}
			case InputEventMouseButton mouseEvent:
			{
				_zoom.Value = mouseEvent.ButtonIndex switch
				{
					MouseButton.WheelUp => _zoom.Value + 0.2,
					MouseButton.WheelDown => _zoom.Value - 0.2,
					_ => _zoom.Value
				};
				switch (mouseEvent.ButtonIndex)
				{
					case MouseButton.Left:
					case MouseButton.Right:
					case MouseButton.Middle:
					{
						if (mouseEvent.ButtonIndex != MouseButton.Left)
						{
							if (mouseEvent.Pressed)
							{
								_lastSelected = _pens.GetSelectedItems()[0];
								_pens.Select(0);
							}
							else
							{
								_pens.Select(_lastSelected);
							}
						}

						_isPressed = mouseEvent.Pressed;
						Tool(Vector2.Zero);
						if (!mouseEvent.Pressed)
						{
							_operatedTiles.Clear();
						}
						break;
					}
				}
				break;
			}
			case InputEventMouseMotion motionEvent:
			{
				if (!_isPressed) return;
				Tool(motionEvent.Relative);
				break;
			}
		}
	}


	private void GetTiles()
	{
		foreach (var tile in Instance.Tiles)
		{
			_selectButton.AddIconItem(tile.Value.Texture,$"{Tr($"Tile_{tile.Key}_Name")}");
			var source = new TileSetAtlasSource();
			source.TextureRegionSize = new Vector2I(32, 32);
			source.Texture = tile.Value.Texture;
			source.CreateTile(Vector2I.Zero);
			_tileIndex.Add(tile.Key, _tileIndex.Count);
			_tiles.TileSet.AddSource(source);
		}
	}

	private void DrawMap()
	{
		var index = 0;
		foreach (var tile in _mapData.MapTiles)
		{
			var pos = new Vector2I((int)(index%_mapData.MapSize.X), (int)Math.Floor(index/_mapData.MapSize.X));
			_tiles.SetCell(pos, _tileIndex[tile], Vector2I.Zero);
			_height.SetCell(pos,0, new Vector2I(0,_mapData.MapHeight[index]));
			index++;
		}
	}

	public void OnZoomValueChanged(float value)
	{
		_camera.Zoom = Vector2.One*(float)Math.Pow(2,value);
	}

	private void Tool(Vector2 relative)
	{
		switch (_pens.GetSelectedItems()[0])
		{
			case 0:
			{
				_camera.GlobalPosition -= relative / _camera.Zoom.X;
				break;	
			}
			case 1:
			{
				if (_mousePosition==new Vector2I(-1,-1))break;
				_tiles.SetCell(_mousePosition, _selectButton.Selected, Vector2I.Zero);
				_mapData.MapTiles[(int)(_mousePosition.X + _mousePosition.Y * _mapData.MapSize.X)] = _tileIndex.Keys.ToImmutableList()[_selectButton.Selected];
				break;
			}
			case 2:
			{
				if (_mousePosition==new Vector2I(-1,-1))break;
				_tiles.SetCell(_mousePosition, _tileIndex["Void"], Vector2I.Zero);
				_mapData.MapTiles[(int)(_mousePosition.X + _mousePosition.Y * _mapData.MapSize.X)] = "Void";
				break;
			}
			case 3:
			{
				_selectButton.Selected =
					_tileIndex[
						_mapData.MapTiles[(int)(_mousePosition.X + _mousePosition.Y * _mapData.MapSize.X)]];
				break;
			}
			case 4:
			{
				var nowTile = (int)(_mousePosition.X + _mousePosition.Y * _mapData.MapSize.X);
				if (!_operatedTiles.Contains(nowTile))
				{
					_mapData.MapHeight[nowTile] = int.Min(_mapData.MapHeight[nowTile]+1,9);
					_height.SetCell(_mousePosition,0, new Vector2I(0,_mapData.MapHeight[nowTile]));
					_operatedTiles.Add(nowTile);
				}
				break;
			}
			case 5:
			{
				var nowTile = (int)(_mousePosition.X + _mousePosition.Y * _mapData.MapSize.X);
				if (!_operatedTiles.Contains(nowTile))
				{
					_mapData.MapHeight[nowTile] = int.Max(_mapData.MapHeight[nowTile]-1,0);
					_height.SetCell(_mousePosition,0, new Vector2I(0,_mapData.MapHeight[nowTile]));
					_operatedTiles.Add(nowTile);
				}
				break;
			}
		}
	}

	public void OnHeightViewChanged(bool value)
	{
		_height.Visible = value;
	}

	public void OnMenuPressed()
	{
		_background.Visible = true;
		_menu.Visible = true;
	}

	public void OnBackPressed()
	{
		_background.Visible = false;
		_menu.Visible = false;
	}

	public void OnSavePressed()
	{
		MapParser.Save(_mapData,Instance.GameMapPath);
		OnBackPressed();
		GetNode<AcceptDialog>("UI/SaveSuccess").Popup();
	}
}
