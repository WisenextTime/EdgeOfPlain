using Godot;
using System;
using System.Drawing;
using Godot.Collections;
using static EdgeOfPlain.Scr.Core.Global.Global;

namespace EdgeOfPlain.Scr.Core.Sences;

public partial class MiniMap3d : Node3D
{
	private GridMap _gridMap;
	public override void _Ready()
	{
		_gridMap = GetNode<GridMap>("GridMap");
		GetTiles();
	}

	private void GetTiles()
	{
		foreach (var tile in Instance.Tiles)
		{
			var texture = tile.Value.Texture.GetImage().Data;
			foreach (var color in (Array<byte>)texture["data"])
			{
				
			}
		}
	}
}