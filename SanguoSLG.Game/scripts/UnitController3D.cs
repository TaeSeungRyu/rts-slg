using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 3D 유닛 토큰. 마우스가 가리키는 헥사를 하이라이트하고 유닛→호버 지점의 A* 경로를 미리 보여준다.
/// 좌클릭하면 그 경로를 따라 트윈으로 이동한다. 경로·이동 규칙은 Core가 소유한다.
/// </summary>
public partial class UnitController3D : Node3D
{
    [Export] public float StepSeconds = 0.16f;

    private MapView3D _view = null!;
    private HexMap _map = null!;
    private MovementService _movement = null!;
    private HexPathfinder _pathfinder = null!;
    private Unit _unit = null!;
    private Camera3D _camera = null!;
    private bool _moving;
    private bool _attacking;

    // 기병 프로시저럴 애니메이션 대상(부위 노드와 기준 자세)
    private readonly List<Node3D> _bodies = new();
    private readonly List<Node3D[]> _legs = new();
    private readonly List<Node3D> _spears = new();
    private readonly List<Vector3> _bodyBasePos = new();
    private readonly List<Vector3> _spearBaseRot = new();
    private float _gallopTime;
    private Vector3 _lastPosition;

    // 하이라이트·경로 오버레이는 유닛과 함께 움직이면 안 되므로 형제 노드에 담는다.
    private Node3D _overlay = null!;
    private MeshInstance3D _hover = null!;
    private readonly List<MeshInstance3D> _pathMarkers = new();
    private HexCoord? _hoverCoord;

    public void Init(HexMap map, MapView3D view, Camera3D camera, Unit unit)
    {
        _map = map;
        _view = view;
        _camera = camera;
        _movement = new MovementService(map);
        _pathfinder = new HexPathfinder(map);
        _unit = unit;
        Position = TokenPosition(unit.Position);
        BuildToken();
        BuildOverlay();

        if (OS.GetCmdlineArgs().Contains("--previewdemo"))
        {
            UpdateHover(new HexCoord(6, 3));
        }
    }

    public override void _Process(double delta)
    {
        // 카메라 조작(팬/회전) 중에는 호버·경로 미리보기를 상태 기반으로 강제 종료 — 깜빡임 방지.
        if (IsCameraManeuvering() && _hoverCoord is not null)
        {
            ClearOverlay();
        }

        AnimateGallop((float)delta);
    }

    // 이동 중 갤럽 모션: 진행 방향으로 회전 + 몸통 바운스 + 다리 스윙(대각 트롯).
    private void AnimateGallop(float dt)
    {
        var moved = Position - _lastPosition;
        _lastPosition = Position;

        if (_moving)
        {
            if (moved.LengthSquared() > 0.000001f)
            {
                var targetYaw = Mathf.Atan2(moved.X, moved.Z);
                Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, targetYaw, 1f - Mathf.Exp(-14f * dt)), 0f);
            }

            _gallopTime += dt * 15f;
            for (var i = 0; i < _bodies.Count; i++)
            {
                var phase = i * 0.8f;
                var bob = Mathf.Abs(Mathf.Sin(_gallopTime + phase)) * 0.030f;
                _bodies[i].Position = _bodyBasePos[i] + new Vector3(0f, bob, 0f);
                _bodies[i].Rotation = new Vector3(Mathf.Sin(_gallopTime + phase) * 0.07f,
                    _bodies[i].Rotation.Y, 0f);

                var swing = Mathf.Sin(_gallopTime + phase) * 0.55f;
                var legs = _legs[i];
                legs[0].Rotation = new Vector3(swing, 0f, 0f);   // 앞왼
                legs[3].Rotation = new Vector3(swing, 0f, 0f);   // 뒤오
                legs[1].Rotation = new Vector3(-swing, 0f, 0f);  // 앞오
                legs[2].Rotation = new Vector3(-swing, 0f, 0f);  // 뒤왼
            }
        }
        else if (_gallopTime != 0f && !_attacking)
        {
            // 정지: 기준 자세로 복귀
            _gallopTime = 0f;
            for (var i = 0; i < _bodies.Count; i++)
            {
                _bodies[i].Position = _bodyBasePos[i];
                _bodies[i].Rotation = new Vector3(0f, _bodies[i].Rotation.Y, 0f);
                foreach (var leg in _legs[i])
                {
                    leg.Rotation = Vector3.Zero;
                }
            }
        }
    }

    /// <summary>공격 모션: 부대 전체가 짧게 돌진하며 창을 앞으로 내지르고 복귀한다.</summary>
    public void PlayAttackMotion()
    {
        if (_moving || _attacking)
        {
            return;
        }

        _attacking = true;
        var forward = new Vector3(Mathf.Sin(Rotation.Y), 0f, Mathf.Cos(Rotation.Y));
        var origin = Position;

        var tween = CreateTween();
        // 1) 창을 수평으로 내림(겨눔)
        foreach (var spear in _spears)
        {
            tween.Parallel().TweenProperty(spear, "rotation:x", _spearBaseRot[_spears.IndexOf(spear)].X + 0.75f, 0.10f);
        }

        // 2) 돌진 → 3) 복귀(창도 원위치)
        tween.Chain().TweenProperty(this, "position", origin + forward * 0.16f, 0.12f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Chain().TweenProperty(this, "position", origin, 0.22f)
            .SetTrans(Tween.TransitionType.Sine);
        for (var i = 0; i < _spears.Count; i++)
        {
            tween.Parallel().TweenProperty(_spears[i], "rotation:x", _spearBaseRot[i].X, 0.18f);
        }

        tween.Finished += () => _attacking = false;
    }

    private static bool IsCameraManeuvering() =>
        Input.IsMouseButtonPressed(MouseButton.Right) ||
        Input.IsMouseButtonPressed(MouseButton.Middle) ||
        Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.A) ||
        Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.D) ||
        Input.IsKeyPressed(Key.Up) || Input.IsKeyPressed(Key.Down) ||
        Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.Right) ||
        Input.IsKeyPressed(Key.Q) || Input.IsKeyPressed(Key.E);

    public override void _UnhandledInput(InputEvent @event)
    {
        // F: 공격 모션 데모(전투 시스템이 생기면 그쪽에서 호출)
        if (@event is InputEventKey { Pressed: true, Keycode: Key.F })
        {
            PlayAttackMotion();
            return;
        }

        if (@event is InputEventMouseMotion motion)
        {
            if (IsCameraManeuvering())
            {
                return;
            }

            if (!_moving && RayToGround(motion.Position) is { } hoverHex)
            {
                UpdateHover(hoverHex);
            }

            return;
        }

        if (_moving || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        if (RayToGround(click.Position) is not { } target)
        {
            return;
        }

        var result = _movement.MoveTo(_unit, target);
        if (result.Moved && result.Path.Count > 1)
        {
            ClearOverlay();
            AnimateAlong(result);
        }
    }

    // 마우스 화면 좌표 → 지면(y=0) → 맵 안의 헥사. 맵 밖이면 null.
    private HexCoord? RayToGround(Vector2 screenPosition)
    {
        var origin = _camera.ProjectRayOrigin(screenPosition);
        var direction = _camera.ProjectRayNormal(screenPosition);
        if (Mathf.Abs(direction.Y) < 0.0001f)
        {
            return null;
        }

        var t = -origin.Y / direction.Y;
        if (t <= 0f)
        {
            return null;
        }

        var coord = _view.WorldToHex(origin + direction * t);
        return _map.Contains(coord) ? coord : null;
    }

    private void UpdateHover(HexCoord coord)
    {
        if (_hoverCoord == coord)
        {
            return;
        }

        _hoverCoord = coord;
        _hover.Visible = true;
        _hover.Position = _view.HexToWorld(coord) + new Vector3(0f, _view.TileTopY + 0.02f, 0f);

        var path = _pathfinder.FindPath(_unit.Position, coord);
        ShowPathMarkers(path);
    }

    private void ShowPathMarkers(IReadOnlyList<HexCoord> path)
    {
        // 시작(유닛 위치)과 끝(호버 하이라이트)은 마커를 생략한다.
        var needed = path.Count > 2 ? path.Count - 2 : 0;
        while (_pathMarkers.Count < needed)
        {
            var marker = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.03f, RadialSegments = 16 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.95f, 0.90f, 0.70f, 0.9f),
                    EmissionEnabled = true,
                    Emission = new Color(0.55f, 0.48f, 0.28f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                },
            };
            _overlay.AddChild(marker);
            _pathMarkers.Add(marker);
        }

        for (var i = 0; i < _pathMarkers.Count; i++)
        {
            var visible = i < needed;
            _pathMarkers[i].Visible = visible;
            if (visible)
            {
                _pathMarkers[i].Position =
                    _view.HexToWorld(path[i + 1]) + new Vector3(0f, _view.TileTopY + 0.03f, 0f);
            }
        }
    }

    private void ClearOverlay()
    {
        _hoverCoord = null;
        _hover.Visible = false;
        foreach (var marker in _pathMarkers)
        {
            marker.Visible = false;
        }
    }

    private void AnimateAlong(MoveResult result)
    {
        _moving = true;
        var tween = CreateTween();
        foreach (var step in result.Path.Skip(1))
        {
            tween.TweenProperty(this, "position", TokenPosition(step), StepSeconds)
                .SetTrans(Tween.TransitionType.Sine);
        }

        tween.Finished += () =>
        {
            _unit = result.Unit;
            _moving = false;
        };
    }

    private Vector3 TokenPosition(HexCoord coord) =>
        _view.HexToWorld(coord) + new Vector3(0f, _view.TileTopY, 0f);

    private void BuildOverlay()
    {
        _overlay = new Node3D();
        GetParent().AddChild(_overlay);

        // 타일과 같은 방향(꼭짓점 ±Z)의 납작한 육각 하이라이트.
        _hover = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = _view.HexWorldSize * 0.94f,
                BottomRadius = _view.HexWorldSize * 0.94f,
                Height = 0.04f,
                RadialSegments = 6,
            },
            Visible = false,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.92f, 0.55f, 0.35f),
                EmissionEnabled = true,
                Emission = new Color(0.6f, 0.52f, 0.25f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
        };
        _overlay.AddChild(_hover);
    }

    // 기병대 모델(3기)을 붙이고, 프로시저럴 애니메이션 대상 부위를 이름으로 수집한다.
    private void BuildToken()
    {
        var instance = GD.Load<PackedScene>("res://assets/models/cavalry.glb").Instantiate<Node3D>();
        AddChild(instance);

        for (var i = 0; i < 3; i++)
        {
            if (instance.FindChild($"u{i}_body", true, false) is not Node3D body)
            {
                continue;
            }

            _bodies.Add(body);
            _bodyBasePos.Add(body.Position);
            _legs.Add(new[]
            {
                (Node3D)instance.FindChild($"u{i}_leg_fl", true, false),
                (Node3D)instance.FindChild($"u{i}_leg_fr", true, false),
                (Node3D)instance.FindChild($"u{i}_leg_bl", true, false),
                (Node3D)instance.FindChild($"u{i}_leg_br", true, false),
            });

            var spear = (Node3D)instance.FindChild($"u{i}_spear", true, false);
            _spears.Add(spear);
            _spearBaseRot.Add(spear.Rotation);
        }

        _lastPosition = Position;
    }
}
