using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Scr.Core.Lib;
using EdgeOfPlain.Scr.Core.Resources;
using Godot;
using Godot.Collections;

namespace EdgeOfPlain.Scr.Core.Sences;

public partial class Navigation : Node
{
	public FlowPathfinding NavigationAgent = new();
	public void GetTileCost(GameTileMap map)
	{
		NavigationAgent.GetTileCost(map);
	}

	public void NewAgent(Array<Node> units,string type,Vector2 position)
	{
		foreach (var unit in units)
		{
			var gameUnit = (GameUnit)unit;
		    gameUnit.NewPath("move",NavigationAgent.GetFlows(type, gameUnit.GlobalPosition, position));
		}
	}
}