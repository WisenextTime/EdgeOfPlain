using System;
using System.Collections.Generic;
using EdgeOfPlain.Scr.Core.Resources;
using Godot;
using EdgeOfPlain.Scr.Core.Lib;
using Godot.Collections;

namespace EdgeOfPlain.Scr.Core.Sences;

public partial class Navigation : Node
{
	public FlowPathfinding NavigationAgent = new FlowPathfinding();
	private List<Dictionary> _agentList = []; 
	public void GetTileCost(GameTileMap map)
	{
		NavigationAgent.GetTileCost(map);
	}

	public void NewAgent(Array<Node> units,string type)
	{
		foreach (var unit in units)
		{
			
		}
	}
}