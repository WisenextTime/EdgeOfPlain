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

	public void NewAgent(GameUnit unit,string type,Vector2 position)
	{
		unit.NewPath("move",NavigationAgent.GetFlows(type, unit.GlobalPosition, position));
	}

	public void NewAttackAgent(GameUnit unit,string type,GameUnit target)
	{
		unit.NewPath("attack",NavigationAgent.GetFlows(type, unit.GlobalPosition, target.GlobalPosition), target);
	}

	public Vector2[] SyncAttackAgent(GameUnit unit, string type, Vector2 target)
	{
		return NavigationAgent.GetFlows(type, unit.GlobalPosition, target);
	}
}