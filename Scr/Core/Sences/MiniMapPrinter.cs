using System;
using System.Linq;
using Godot;

namespace EdgeOfPlain.Scr.Core.Sences;

public partial class MiniMapPrinter : Node2D
{
	private MiniMap _parent;
	public override void _Draw()
	{
		_parent = GetParent<MiniMap>();
		var mapData = _parent.Parent.TileMap;
		foreach (var tile in mapData.MapTiles.Select((Value, Index) => new { Value, Index }))
		{
			var pos = new Vector2I((int)(tile.Index % mapData.MapSize.X), (int)Math.Floor(tile.Index / mapData.MapSize.X));
			DrawRect(new Rect2(pos,Vector2.One),_parent.TileIndex[tile.Value]);
		}
	}
}