using System;
using System.Threading.Tasks;
using EdgeOfPlain.Scr.Core.Lib;
using EdgeOfPlain.Scr.Core.Resources;
using Godot;
using Godot.Collections;
using static EdgeOfPlain.Scr.Core.Global.Global;

namespace EdgeOfPlain.Scr.Core.Sences;

public partial class MiniMap : SubViewport
{ 
	private Camera2D _camera;
	private TileMapLayer _tileMap;
	private System.Collections.Generic.Dictionary<string,int> _tileIndex = [];
	private GameTileMap _mapData;
	private Line2D _rect;
	private Vector2 _windowSize;
	private Game _parent;

	public override async void _Ready()
	{
		_parent = GetTree().Root.GetNode<Game>("Game");
		await ToSignal(_parent,Node.SignalName.Ready);
		_mapData = _parent.TileMap;
		_tileMap = GetNode<TileMapLayer>("Minimap");
		_camera = GetNode<Camera2D>("Camera");
		_rect = GetNode<Line2D>("Rect");
		_windowSize = GetTree().Root.Size;
		GetTiles();
		DrawMap();
		_camera.Position = new Vector2(_mapData.MapSize.X/2, _mapData.MapSize.Y/2);
		var zoom = Math.Min(Size.X / _mapData.MapSize.X, Size.Y / _mapData.MapSize.Y);
		_camera.Zoom = new Vector2(zoom, zoom);
	}

	private void GetTiles()
	{
		_tileMap.TileSet = new TileSet();
		_tileMap.TileSet.TileSize = Vector2I.One;
		foreach (var tile in Instance.Tiles)
		{
			var source = new TileSetAtlasSource();
			source.TextureRegionSize = new Vector2I(1, 1);
			var color = ImageLoader.GetColor(tile.Value.Texture);
			var newImage = new GradientTexture1D{Width = 1,Gradient = new Gradient{Colors = [color,color],Offsets = [0,1]}};
			//GD.Print(newImage.Gradient.GetColors());
			source.Texture = (Texture2D)newImage.Duplicate();
			source.CreateTile(Vector2I.Zero);
			_tileIndex.Add(tile.Key, _tileIndex.Count);
			_tileMap.TileSet.AddSource(source);
		}
		ResourceSaver.Save(_tileMap.TileSet,"res://Game.tres");
	}
	
	private void DrawMap()
	{
		var index = 0;
		foreach (var tile in _mapData.MapTiles)
		{
			var pos = new Vector2I((int)(index%_mapData.MapSize.X), (int)Math.Floor(index/_mapData.MapSize.X));
			_tileMap.SetCell(pos, _tileIndex[tile], Vector2I.Zero);
			index++;
		}
	}

	public override void _Process(double delta)
	{
		_rect.ClearPoints();
		var tureSize = _windowSize / _parent.GameCamera.Zoom.X / 32;
		var truePosition = _parent.GameCamera.Position / 32;
		_rect.AddPoint(new Vector2(truePosition.X-tureSize.X/2, truePosition.Y-tureSize.Y/2));
		_rect.AddPoint(new Vector2(truePosition.X-tureSize.X/2, truePosition.Y+tureSize.Y/2));
		_rect.AddPoint(new Vector2(truePosition.X+tureSize.X/2, truePosition.Y+tureSize.Y/2));
		_rect.AddPoint(new Vector2(truePosition.X+tureSize.X/2, truePosition.Y-tureSize.Y/2));
	}
	
}