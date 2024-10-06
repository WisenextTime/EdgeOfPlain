using Godot;
using System;
using System.Collections.Generic;
using EdgeOfPlain.Scr.Core.Lib;
using EdgeOfPlain.Scr.Core.Resources;
using static EdgeOfPlain.Scr.Core.Global.Global;

namespace EdgeOfPlain.Scr.Core.Sences;
public partial class GameUnit : CharacterBody2D
{
	public Unit UnitData { get; set; } = Instance.Units["NNSA/BaseInfantry"];
	private AnimatedSprite2D _image;
	private CollisionShape2D _shape;
	private NavigationAgent2D _navigation;
	private CollisionShape2D _final;
	private Area2D _area;
	
	private uint _baseCollision;
	private float _nowHeight;

	private float _speed;
	private float _rotateSpeed;

	private TileMapLayer _map;
	
	private Vector2 _nowTarget = Vector2.Zero;
	
	
	public override void _Ready()
	{
		AddToGroup("Unit");
		AddToGroup(UnitData.UnitMoveType switch
		{
			UnitMoveType.None => "none",
			UnitMoveType.Ground => "ground" + UnitData.TargetHeight.GetType(),
			UnitMoveType.Water => "water",
			UnitMoveType.Air => "air",
			UnitMoveType.Hover => "hover",
			UnitMoveType.Any => "any",
			_ => throw new Exception()
		});
		_image = GetNode<AnimatedSprite2D>("Image");
		_shape = GetNode<CollisionShape2D>("Shape");
		_navigation = GetNode<NavigationAgent2D>("Navigation");
		_navigation.TargetPosition = GlobalPosition;
		_map = GetNode<TileMapLayer>("../../Tiles");
		_final = GetNode<CollisionShape2D>("Final/Shape");
		_final.Shape = new CircleShape2D { Radius = UnitData.Radius * 8 };
		_area = GetNode<Area2D>("Final");

		_image.SpriteFrames = UnitData.Texture;
		_shape.Shape = new CircleShape2D{Radius = UnitData.Radius};
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
		uint heightData = 1;
		if (UnitData.UnitMoveType is UnitMoveType.Ground or UnitMoveType.Hover)
		{
			for (var _ = 0; _! < UnitData.TargetHeight; _++)
			{
				heightData <<= 1;
			}
		} 
		_navigation.NavigationLayers = UnitData.UnitMoveType switch
		{
			UnitMoveType.Any => 0b_111_00_11111_11111,
			UnitMoveType.Water => 0b_010_00_11111_11111,
			UnitMoveType.Air => 0b_100_00_11111_11111,
			UnitMoveType.Hover => 0b_011_00_00000_00000 + heightData,
			UnitMoveType.Ground => 0b_001_00_00000_00000 + heightData,
			_ => 0b_000_00_00000_00000
		};
	}

	public override void _PhysicsProcess(double delta)
	{
	}

	private void SetLayer()
	{
		CollisionMask = _nowHeight switch
		{
			< -5 => _baseCollision | 0b_001_000_00,
			> 10 => _baseCollision | 0b_100_000_00,
			_ => _baseCollision | 0b_010_000_00,
		};
		CollisionLayer = _nowHeight switch
		{
			< -5 => 0b_001_000_00,
			> 10 => 0b_100_000_00,
			_ => 0b_010_000_00,
		};
	}
}
