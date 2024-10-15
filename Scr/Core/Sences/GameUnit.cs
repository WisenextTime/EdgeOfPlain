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

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selectedLine.Visible = value;
            _hpBar.Visible = value;
            _selected = value;
        }
    }
    private bool _selected;
    public Queue<Vector2> Path;
    public GameUnit Target;

    public UnitState State = UnitState.Idle;

    private float _rotateSpeed;
    private float _speed;
    private float _rough;

    public float TargetHeight;
    public float TrueHeight;
    private uint OriginMask;

    public bool TouchedMouse;

    /// <summary>
    /// 移动失败计数器
    /// </summary>
    private int _faultCount;

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
        _hpBar.Visible = false;

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


        var shader = (ShaderMaterial)_sprite.Material;
        shader.SetShaderParameter("TeamId", TeamId);


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
        _wayLine.ClearPoints();
        switch (State)
        {
            case UnitState.Move:
                if (Path == null) return;
                _wayLine.DefaultColor = Colors.Blue;
                _wayLine.AddPoint(Vector2.Zero);
                //_wayLine.AddPoint(ToLocal(Path.Peek()));
                foreach (var p in Path)
                {
                    _wayLine.AddPoint(ToLocal(p));
                }
                break;
            case UnitState.Attack:
                if (Target == null) return;
                _wayLine.DefaultColor = Colors.Red;
                _wayLine.AddPoint(Vector2.Zero);
                _wayLine.AddPoint(ToLocal(Target.GlobalPosition));
                break;
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

    public void NewWayPoint(UnitState state, Array args)
    {
        if (State == UnitState.Freezed || !Selected) return;
        switch (state)
        {
            case UnitState.Attack:
                Target = (GameUnit)args[0];
                State = UnitState.Attack;
                break;
            case UnitState.Defend:
                break;
            case UnitState.MoveAttack:
                break;
            case UnitState.LoadIn:
                break;
            case UnitState.Deploy:
                break;
            default:
                var target = (Vector2)args[0];
                State = UnitState.Move;
                Vector2[] pointsCollection = _game.Navigation.GetPath(GlobalPosition, target, UnitData.UnitMoveType);
                Path = pointsCollection is null ? null : new(pointsCollection);
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
            case UnitState.Idle:
                _sprite.Play("Idle");
                break;
            case UnitState.Move:
                if (Path == null) State = UnitState.Idle;
                else
                {
                    PathCheck();
                    Move((float)delta);

                    //存在一个不是十分小的速度
                    if (Velocity != Vector2.Zero && GetRealVelocity().LengthSquared() < (Velocity / 10).LengthSquared())
                    {
                        if (Path.Count == 1 && (ToLocal(Path.Peek()).Length() < UnitData.Radius * 5))
                        {
                            Path = null;
                        }
                        else
                        {
                            if (++_faultCount > 100)
                            {
                                Path.Dequeue();
                                if (Path.Count == 0) Path = null;
                                _faultCount = 0;
                            }
                        }
                    }
                }
                _sprite.Play("Move");
                break;
            case UnitState.Attack:
                if (Target == null)
                {
                    State = UnitState.Idle;
                    return;
                }
                Path = new Queue<Vector2>(_game.Navigation.GetPath(GlobalPosition, Target.GlobalPosition, UnitData.UnitMoveType));
                if (ToLocal(Target.GlobalPosition).Length() > UnitData.AttackRange)
                {
                    Move((float)delta);
                    _sprite.Play("Move");
                }
                break;
        }
        var otherCollision = MoveAndCollide(Velocity / 5, true);
        if (otherCollision?.GetCollider() is not CharacterBody2D otherUnit) MoveAndSlide();
        else if (Path is not null && Path.Count > 0)
        {
            if (++_faultCount <= 100) return;
            Path.Dequeue();
            if (Path.Count == 0) Path = null;
            _faultCount = 0;
        }
    }

    private void PathCheck()
    {
        if (ToLocal(Path.Peek()).LengthSquared() < UnitData.Radius * UnitData.Radius)
        {
            _ = Path.Dequeue();
        }

        if (Path.Count == 0)
            Path = null;
    }

    private void Move(float delta)
    {
        if (Path == null || Path.Count == 0) return;
        var nextPos = Path.Peek();
        var nextPoint = _sprite.ToLocal(nextPos);

        var abstractPosition = ExactMath.GetAbstractPosition(GlobalPosition, nextPos);
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
        _hpBar.Visible = true;
    }

    public void MouseExit()
    {
        TouchedMouse = false;
        RemoveFromGroup("Selected");
        _hpBar.Visible = _selected;
    }
}

public enum UnitState
{
    Idle,
    Move,
    Attack,
    Defend,
    MoveAttack,
    Freezed,
    LoadIn,
    Deploy,
}
