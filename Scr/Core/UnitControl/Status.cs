using System.Collections.Generic;
using System.Linq;
using EdgeOfPlain.Scr.Core.Sences;
using Godot;
using Godot.Collections;

namespace EdgeOfPlain.Scr.Core.UnitControl;

public partial class Status : Node
{
	private GameUnit _parent;
	public List<KeyValuePair<string, object>> StatusList = [];

	public override void _Ready()
	{
		_parent = GetNode<GameUnit>("..");
	}

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
					_parent.NowStatus = "idle";
					_parent.Path = [];
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
				_parent.NowStatus = "moving";
				
				var nextPoint = (Vector2[])StatusList[0].Value;
				if (_parent.ToLocal(nextPoint[0]).Length() < _parent.UnitData.Radius
				    || _parent.GetRealVelocity().Length() < _parent.Velocity.Length() / 10)
				{
					_parent.Velocity = _parent.GetRealVelocity();
					if (nextPoint.Length == 1)
					{
						StatusList.RemoveAt(0);
						StatusList.Add(new KeyValuePair<string, object>("idle", 0));
						return;
					}

					nextPoint = nextPoint.Skip(1).ToArray();
					StatusList[0] = new KeyValuePair<string, object>("moving", nextPoint);
				}
				_parent.Path = (Vector2[])StatusList[0].Value;
				break;
			}
			case "attacking":
			{
				if (StatusList.Count != 1)
				{
					StatusList.RemoveAt(0);
					return;
				}
				_parent.NowStatus = "attacking";
				var target = (GameUnit)StatusList[0].Value;
				_parent.Path = GetTree().Root.GetNode<Game>("Game").Navigation.SyncAttackAgent(_parent, _parent.MoveType, target.GlobalPosition).Skip(1).ToArray();
				break;
			}
		}
	}
}