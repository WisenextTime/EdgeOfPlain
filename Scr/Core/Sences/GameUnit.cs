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

namespace EdgeOfPlain.Scr.Core.Sences;
public partial class GameUnit : CharacterBody2D
{
	public int TeamId;
	public bool Selected;
	public Unit UnitData { get; set; } = Instance.Units["NNSA/BaseInfantry"];
	private AnimatedSprite2D _image;
	private CollisionShape2D _shape;
	private Line2D _wayTarget;
	private Status _status;
	private Line2D _selectedLine;
	
	private uint _baseCollision;
	private float _nowHeight;

	private float _speed;
	private float _rotateSpeed;

	private TileMapLayer _map;
	
	private Vector2 _nowTarget = Vector2.Zero;
	public Vector2[] Path = [];

	public string NowStatus = "idle";
	private float _rough = 1f;

	public string MoveType => UnitData.UnitMoveType switch
	{
		UnitMoveType.None => "none",
		UnitMoveType.Ground => $"land{UnitData.AccessableHeight}",
		UnitMoveType.Water => "water",
		UnitMoveType.Air => "air",
		UnitMoveType.Hover => "hover",
		_ => "any",
	};
	
	public override void _Ready()
	{
		AddToGroup("Unit");
		AddToGroup(UnitData.UnitMoveType switch
		{
			UnitMoveType.None => "none",
			UnitMoveType.Ground => "land" + GD.VarToStr(UnitData.AccessableHeight),
			UnitMoveType.Water => "water",
			UnitMoveType.Air => "air",
			UnitMoveType.Hover => "hover",
			UnitMoveType.Any => "any",
			_ => throw new Exception()
		});
		_image = GetNode<AnimatedSprite2D>("Image");
		_shape = GetNode<CollisionShape2D>("Shape");
		_wayTarget = GetNode<Line2D>("WayPoint");
		_status = GetNode<Status>("Status");
		_status.StatusList.Add(new KeyValuePair<string, object>("idle",0));
		_map =GetTree().Root.GetNode<TileMapLayer>("Game/Tiles");
		
		_image.SpriteFrames = UnitData.Texture;
		_shape.Shape = new CircleShape2D{Radius = UnitData.Radius};
		
		_selectedLine = GetNode<Line2D>("SelectedLine");
		_selectedLine.AddPoint(new Vector2(0, UnitData.Radius * 1.414f));
		_selectedLine.AddPoint(new Vector2(UnitData.Radius * 1.414f, 0));
		_selectedLine.AddPoint(new Vector2(0, -UnitData.Radius * 1.414f));
		_selectedLine.AddPoint(new Vector2(-UnitData.Radius * 1.414f, 0));
		_selectedLine.Visible = false;
		
		_baseCollision = CollisionMask = UnitData.UnitMoveType switch
		{
			UnitMoveType.Any => 0b_000_000_00,
			UnitMoveType.Water => 0b_000_010_00,
			UnitMoveType.Air => 0b_000_100_00,
			UnitMoveType.Ground => 0b_000_001_00,
			UnitMoveType.Hover => 0b_1_00000_00000_00000_00000,
			_ => 0b_000_111_00,
		};
		//Mass = UnitData.Mass;
		_nowHeight = UnitData.TargetHeight;

		_speed = UnitData.MoveSpeed;
		_rotateSpeed = UnitData.RotationSpeed;
		SetLayer();
		//uint heightData = 1;
		//if (UnitData.UnitMoveType is UnitMoveType.Ground or UnitMoveType.Hover)
		//{
		//	for (var _ = 0; _! < UnitData.TargetHeight; _++)
		//	{
		//		heightData <<= 1;
		//	}
		//} 
		/*_navigation.NavigationLayers = UnitData.UnitMoveType switch
		{
			UnitMoveType.Any => 0b_111_00_11111_11111,
			UnitMoveType.Water => 0b_010_00_11111_11111,
			UnitMoveType.Air => 0b_100_00_11111_11111,
			UnitMoveType.Hover => 0b_011_00_00000_00000 + heightData,
			UnitMoveType.Ground => 0b_001_00_00000_00000 + heightData,
			_ => 0b_000_00_00000_00000
		};
		*/
	}

	private void SetLayer()
	{
		CollisionMask = _nowHeight switch
		{
			< -5 => _baseCollision | 0b_001_000_01,
			> 10 => _baseCollision | 0b_100_000_01,
			_ => _baseCollision | 0b_010_000_01,
		};
		CollisionLayer = _nowHeight switch
		{
			< -5 => 0b_001_000_00,
			> 10 => 0b_100_000_00,
			_ => 0b_010_000_00,
		};
	}

	public void NewPath(string type,Vector2[] path)
	{
		switch (type)
		{
			case "move":
			{
				_status.StatusList.Add(new KeyValuePair<string, object>("moving",path.ToArray()));
				break;
			}
		}
	}

	public override void _Process(double delta)
	{
		_wayTarget.ClearPoints();
		switch (NowStatus)
		{
			case "moving" :
				_wayTarget.DefaultColor = Colors.Green;
				_wayTarget.AddPoint(Vector2.Zero);
				_wayTarget.AddPoint(ToLocal(Path[^1]));
				break;
			case "attacking" :
				_wayTarget.DefaultColor = Colors.Red;
				_wayTarget.AddPoint(Vector2.Zero);
				_wayTarget.AddPoint(ToLocal(Path[^1]));
				break;
		}

		_selectedLine.Visible = Selected;
	}

	public override void _PhysicsProcess(double delta)
	{
		_rough = (float)_map.GetCellTileData((Vector2I)(GlobalPosition/32)).GetCustomData("Rough");
		Velocity *= _rough * 0.5f;
		switch (NowStatus) 
		{ 
			case "moving" :
				Move((float)delta);
				//Debug();
				MoveAndSlide();
				break; 
			case "attacking" : 
				break;
		}
		
	}

	private void Move(float delta)
	{
		var nextPoint = ToLocal(Path[0]);
		
		var abstractPosition = ExactMath.GetAbstractPosition(GlobalPosition, Path[0]);
		if (Math.Abs(nextPoint.Angle()) > 2 * _rotateSpeed * delta)
		{
			Rotation += _rotateSpeed * delta * (nextPoint.Angle() > 0 ? 0.5f : -0.5f);
		}
		else
		{
			Velocity += abstractPosition.Normalized() * _speed * delta / _rough * 600;
		}
	}

	private void Debug()
	{
		var debugLine = GetNode<Line2D>("Debug");
		debugLine.ClearPoints();
		foreach (var point in Path)
		{
			debugLine.AddPoint(ToLocal(point));
		}
	}

	public void Unselect(int team)
	{
		if (team != TeamId) return;
		Selected = false;
	}

	public void Select(int team, Rect2 range)
	{
		if (team != TeamId) return;
		if (!range.HasPoint(GlobalPosition)) return;
		Selected = true;
	}

	public void MoveToTarget(Vector2 target)
	{
		if (!Selected) return;
		GetTree().Root.GetNode<Game>("Game").Navigation.NewAgent(this,MoveType,target);
	}
}
