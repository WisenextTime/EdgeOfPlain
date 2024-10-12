using System;
using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Scr.Core.Lib;
using EdgeOfPlain.Scr.Core.Resources;
using Godot;
using Godot.Collections;

namespace EdgeOfPlain.Scr.Core.Sences;

public partial class Navigation : Node
{
	private Game _parent;
	public FlowPathfinding FlowPathfinding;

	public override async void _Ready()
	{
		_parent = GetTree().Root.GetNode<Game>("Game");
		await ToSignal(_parent, Node.SignalName.Ready);
		FlowPathfinding = new FlowPathfinding
			(new Vector2I((int)_parent.TileMap.MapSize.X, (int)_parent.TileMap.MapSize.Y),_parent.TileMap);
	}

	public Vector2[] GetPath(Vector2 fromPos, Vector2 toPos,Global.Global.UnitMoveType type)
	{
		var stringType = type switch
		{
			Global.Global.UnitMoveType.Ground => "Land",
			Global.Global.UnitMoveType.Water => "Water",
			Global.Global.UnitMoveType.Air => "Air",
			Global.Global.UnitMoveType.Hover => "Hover",
			Global.Global.UnitMoveType.Any => "ANy",
			_ => "None"
		};
		return FlowPathfinding.GetPath(fromPos, toPos, stringType);
	}
}