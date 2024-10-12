using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdgeOfPlain.Scr.Core.Lib;
using EdgeOfPlain.Scr.Core.Resources;
using EdgeOfPlain.Scr.Core.UnitControl;
using Godot.Collections;
using static EdgeOfPlain.Scr.Core.Global.Global;
using Array = Godot.Collections.Array;

namespace EdgeOfPlain.Scr.Core.Sences;

public partial class GameUnit : CharacterBody2D
{
	public int TeamId = -1;
	public int TeamGroup = -1;

	public Unit UnitData = Instance.Units["NNSA/BaseInfantry"];
	
	private AnimatedSprite2D _sprite;
	private CollisionShape2D _shape;
	private Game _game;
	private Line2D _selectedLine;

	public bool Selected;
	public Vector2[] Path;

	public string State = "Idle";

	public override void _Ready()
	{
		_game = GetNode<Game>("/root/Game");
		_sprite = GetNode<AnimatedSprite2D>("Image");
		_sprite.SpriteFrames = UnitData.Texture;
		_shape = GetNode<CollisionShape2D>("Shape");
		_shape.Shape = new CircleShape2D { Radius = UnitData.Radius };
		_selectedLine = GetNode<Line2D>("SelectedLine");
		_selectedLine.AddPoint(Vector2.Up * UnitData.Radius);
		_selectedLine.AddPoint(Vector2.Left * UnitData.Radius);
		_selectedLine.AddPoint(Vector2.Down * UnitData.Radius);
		_selectedLine.AddPoint(Vector2.Right * UnitData.Radius);
	}

	public override void _Process(double delta)
	{
		_selectedLine.Visible = Selected;
		if (TeamId == _game.TeamId)
		{
			_selectedLine.DefaultColor = Colors.LimeGreen;
		}
		else if (TeamGroup == _game.TeamGroup)
		{
			_selectedLine.DefaultColor = Colors.Yellow;
		}
		else if (TeamId == -1)
		{
			_selectedLine.DefaultColor = Colors.White;
		}
		else
		{
			_selectedLine.DefaultColor = Colors.Red;
		}
	}

	public void OnSelected(Rect2 range)
	{
		if (!range.HasPoint(GlobalPosition) || TeamId != _game.TeamId) return;
		Selected = true;
	}

	public void OnDeselected(Rect2 range)
	{
		if (range.HasPoint(GlobalPosition)) return;
		Selected = false;
	}

	public GameUnit OnTarget(Rect2 range)
	{
		return range.HasPoint(GlobalPosition) ? this : null;
	}

	public void NewWayPoint(string type, Array args)
	{
		if (State == "Freezed" || !Selected) return;
		switch (type)
		{
			case "attack":
				break;
			case  "defend":
				break;
			case "MoveAttack":
				break;
			case "loadIn":
				break;
			case "deploy":
				break;
			default:
				var target = (Vector2)args[0];
				Path = _game.Navigation.GetPath(GlobalPosition, target,UnitData.UnitMoveType);
				break;
		}
	}
}
