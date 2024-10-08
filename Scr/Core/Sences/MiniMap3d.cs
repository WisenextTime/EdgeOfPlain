using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Godot.Collections;
using static EdgeOfPlain.Scr.Core.Global.Global;
using EdgeOfPlain.Scr.Core.Lib;
using EdgeOfPlain.Scr.Core.Resources;
using Vector3  = Godot.Vector3;

namespace EdgeOfPlain.Scr.Core.Sences;

public partial class MiniMap3d : Node3D
{
	private GridMap _gridMap;
	private System.Collections.Generic.Dictionary<string, int> _meshIndex = [];
	
	public override void _Ready()
	{
		_gridMap = GetNode<GridMap>("GridMap");
		GetTiles();
	}

	private void GetTiles()
	{
		var library = new MeshLibrary();
		foreach (var tile in Instance.Tiles.Select((tile, index) => new { tile.Value, index, tile.Key }))
		{
			var texture = tile.Value.Texture.GetImage().Data;
			var colors = (Array<int>)texture["data"];
			List<Vector3> packedColor =[];
			//packedColor.AddRange(colors.Select((_, i) => new Vector3(colors[3 * i], colors[3 * i + 1], colors[3 * i + 2])));
			for (var i = 0; i <colors.Count/3; i++)
			{
				packedColor.Add(new Vector3(colors[3*i], colors[3*i + 1], colors[3*i + 2]));
			}
			System.Collections.Generic.Dictionary<Vector3, int> colorList = [];
			foreach (var color in packedColor.Select((color,index) => new { color, index }))
			{
				if (colorList.ContainsKey(packedColor[color.index]))
				{
					colorList[color.color]++;
				}
				else
				{
					colorList.Add(color.color, 1);
				}
			}

			var finalColor = colorList.OrderByDescending(color => color.Value).First().Key;
			var block = new BoxMesh();
			var blockMaterial = new ShaderMaterial();
			blockMaterial.Shader = ResourceLoader.Load<Shader>("res://Res/Shaders/3DColor.gdshader");
			blockMaterial.SetShaderParameter("Color",new Vector4(finalColor.X/255,finalColor.Y/255,finalColor.Z/255,1));
			block.Material = blockMaterial;
			library.CreateItem(tile.index);
			library.SetItemMesh(tile.index, block);
			library.SetItemName(tile.index, tile.Key);
			_meshIndex.Add(tile.Key,tile.index);
		}
		_gridMap.MeshLibrary = library;
		var map = MapParser.Load(Instance.GameMapPath);
		var index = 0;
		foreach (var tile in map.MapTiles)
		{
			_gridMap.SetCellItem(new Vector3I((int)(index%map.MapSize.X),map.MapHeight[index],(int)(index/map.MapSize.X)),_meshIndex[tile]);
			index++;
		}
	}
}