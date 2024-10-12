using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
	private Line2D _wayLine;
	private ProgressBar _hpBar;

	public bool Selected;
	public Vector2[] Path;
	public GameUnit Target;

	public string State = "Idle";

	private float _rotateSpeed;
	private float _speed;
	private float _rough;

	public float TargetHeight;
	public float TrueHeight;
	private uint OriginMask;

	public bool TouchedMouse;
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
		_wayLine = GetNode<Line2D>("WayLine");
		_hpBar = GetNode<ProgressBar>("HpBar");
		
		_rotateSpeed = UnitData.RotationSpeed;
		_speed = UnitData.MoveSpeed;
		OriginMask = UnitData.UnitMoveType switch
		{
			UnitMoveType.None => 0b_000_111100,
			UnitMoveType.Ground => 0b_000_100100,
			UnitMoveType.Water => 0b_000_101000,
			UnitMoveType.Air => 0b_000_010000,
			UnitMoveType.Hover => 0b_000_100000,
			_ => 0b_000_000000
		};
		SetHeight();
	}

	private void SetHeight()
	{
		TrueHeight = TargetHeight;
		CollisionLayer = TrueHeight switch
		{
			< 0 => 0b_010_000000,
			> 10 => 0b_100_000000,
			_ => 0b_001_000000
		};
		CollisionMask = OriginMask + CollisionLayer;
	}

	public override void _Process(double delta)
	{
		_selectedLine.Visible = Selected;
		var shader = (ShaderMaterial)_sprite.Material;
		shader.SetShaderParameter("TeamId",TeamId);
		if (TeamId == _game.TeamId)
		{
			_selectedLine.DefaultColor = Colors.LimeGreen;
			_hpBar.Modulate = Colors.LimeGreen;
		}
		else if (TeamGroup == _game.TeamGroup)
		{
			_selectedLine.DefaultColor = Colors.Yellow;
			_hpBar.Modulate = Colors.Yellow;
		}
		else if (TeamId == -1)
		{
			_selectedLine.DefaultColor = Colors.White;
			_hpBar.Modulate = Colors.White;
		}
		else
		{
			_selectedLine.DefaultColor = Colors.Red;
			_hpBar.Modulate = Colors.Red;
		}
		_wayLine.ClearPoints();
		switch (State)
		{
			case "Move":
			{
				if (Path == null) return;
				_wayLine.DefaultColor = Colors.Blue;
				_wayLine.AddPoint(Vector2.Zero);
				_wayLine.AddPoint(ToLocal(Path[^1]));
				break;
			}
			case "Attack":
			{
				if (Target == null) return;
				_wayLine.DefaultColor = Colors.Red;
				_wayLine.AddPoint(Vector2.Zero);
				_wayLine.AddPoint(ToLocal(Target.GlobalPosition));
				break;
			}
		}
		_hpBar.Visible = IsInGroup("Selected") || Selected;
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
	public void NewWayPoint(string type, Array args)
	{
		if (State == "Freezed" || !Selected) return;
		switch (type)
		{
			case "Attack":
				Target = (GameUnit)args[0];
				State = "Attack";
				break;
			case  "Defend":
				break;
			case "MoveAttack":
				break;
			case "LoadIn":
				break;
			case "Deploy":
				break;
			default:
				var target = (Vector2)args[0];
				Path = _game.Navigation.GetPath(GlobalPosition, target,UnitData.UnitMoveType);
				State = "Move";
				break;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		_rough = (float)_game.Tiles.GetCellTileData((Vector2I)(GlobalPosition / 32)).GetCustomData("Rough");
		Velocity *= _rough * 0.5f;
		if (Math.Abs(TrueHeight - TargetHeight) > 1)
		{
			SetHeight();
		}
		switch (State)
		{
			case "Idle":
			{
				_sprite.Play("Idle");
				break;
			}
			case "Move":
			{
				if (Path == null)
				{
					State = "Idle";
				}
				else
				{
					PathCheck();
					Move((float)delta);

					if (Velocity != Vector2.Zero && Velocity / 10 > GetRealVelocity())
					{
						if (ToLocal(Path[^1]).Length() < UnitData.Radius * 5)
						{
							Path = null;
						}
						else
						{
							NewWayPoint("move", [Path[^1]]);
						}
					}
				}
				_sprite.Play("Move");
				break;
			}
			case "Attack":
			{
				if (Target == null)
				{
					State = "Idle";
					return;
				}
				Path = _game.Navigation.GetPath(GlobalPosition, Target.GlobalPosition,UnitData.UnitMoveType);
				if (ToLocal(Target.GlobalPosition).Length() > UnitData.AttackRange)
				{
					Move((float)delta);
					_sprite.Play("Move");
				}
				break;
			}
		}
		MoveAndSlide();
	}

	private void PathCheck()
	{
		if (ToLocal(Path[0]).Length() < UnitData.Radius)
		{
			Path = Path.Skip(1).ToArray();
		}
		if (Path.Length == 0)
		{
			Path = null;
		}
	}

	private void Move(float delta)
	{
		if(Path==null || Path.Length == 0) return;
		var nextPoint = _sprite.ToLocal(Path[0]);
		
		var abstractPosition = ExactMath.GetAbstractPosition(GlobalPosition, Path[0]);
		if (Math.Abs(nextPoint.Angle()) > 2 * _rotateSpeed * delta)
		{
			_sprite.Rotation += _rotateSpeed * delta * (nextPoint.Angle() > 0 ? 0.5f : -0.5f);
		}
		else
		{
			Velocity += abstractPosition.Normalized() * _speed * delta / _rough * 600;
		}
	}

	public void MouseEnter()
	{
		TouchedMouse = true;
		AddToGroup("Selected");
	}

	public void MouseExit()
	{
		TouchedMouse = false;
		RemoveFromGroup("Selected");
	}
}
