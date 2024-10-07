using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Scr.Core.Sences;
using Godot;
using Godot.Collections;

namespace EdgeOfPlain.Scr.Core.UnitControl;

public partial class Status : Node
{
	public List<KeyValuePair<string, object>> StatusList = [];

	public override void _PhysicsProcess(double delta)
	{
		switch (StatusList[0].Key)
		{
			case "frozen":
			{
				break;
			}
			case "idle":
			{
				if (StatusList.Count == 1)
				{
					GetNode<GameUnit>("..").NowStatus = "idle";
					GetNode<GameUnit>("..").Path = [];
					return;
				}
				StatusList.RemoveAt(0);
				break;
			}
			case "moving":
			{
				if (StatusList.Count != 1)
				{
					StatusList.RemoveAt(0);
					return;
				}
				GetNode<GameUnit>("..").NowStatus = "moving";
				var nextPoint = (Vector2[])StatusList[0].Value;
				if(GetNode<GameUnit>("..").ToLocal(nextPoint[0]).Length() < GetNode<GameUnit>("..").UnitData.Radius)
				{
					if (nextPoint.Length == 1)
					{
						StatusList.RemoveAt(0);
						StatusList.Add(new KeyValuePair<string, object>( "idle",0));
						return;
					}
					nextPoint= nextPoint.Skip(1).ToArray();
					StatusList[0] = new KeyValuePair<string, object>("moving", nextPoint);
				}
				GetNode<GameUnit>("..").Path = (Vector2[])StatusList[0].Value;
				break;
			}
		}
	}
}