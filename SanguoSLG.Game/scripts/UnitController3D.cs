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
    /// <summary>한 타일을 건너는 데 걸리는 시간. 병종 데이터가 생기면 이동력에 따라 달라진다.</summary>
    [Export] public float StepSeconds = 0.36f;

    // 보폭: 이동 거리 1당 다리 주기가 도는 각도. 시간이 아니라 거리에 물려야
    // StepSeconds를 바꿔도 발이 지면에서 미끄러지지 않는다.
    private const float MarchRadiansPerUnit = 27f;

    private MapView3D _view = null!;
    private HexMap _map = null!;
    private MovementService _movement = null!;
    private HexPathfinder _pathfinder = null!;
    private Unit _unit = null!;
    private Camera3D _camera = null!;
    private bool _moving;
    private bool _attacking;

    // 편대 검수용 임시 지정 — 병종 데이터(data/troop-types.json)가 생기면 그쪽에서 받는다.
    private const string TroopModel = "res://assets/models/troop-swordsman.glb";
    private const int TroopCount = 7;

    // 보병 프로시저럴 애니메이션 대상. 편대원마다 부위 노드와 기준 자세를 들고 있다.
    private sealed class Member
    {
        public Node3D Body = null!;
        public Node3D LegL = null!;
        public Node3D LegR = null!;
        public Node3D ArmL = null!;
        public Node3D ArmR = null!;
        public Vector3 BodyBasePosition;
        public Vector3 BodyBaseRotation;
        public Vector3 ArmRBaseRotation;
        public float Phase;

        /// <summary>공격 시작 지연 — 앞줄부터 차례로 친다.</summary>
        public float AttackDelay;

        /// <summary>상체를 트는 방향(±1) — 편대원끼리 엇갈리게 한다.</summary>
        public float TwistSign;
    }

    private readonly List<Member> _members = new();
    private float _marchTime;
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

        AnimateMarch((float)delta);
    }

    // 이동 중 행군 모션: 진행 방향으로 회전 + 몸통 상하 흔들림 + 다리·팔 교차 스윙.
    // 편대원마다 위상을 어긋나게 줘 발이 한꺼번에 떨어지지 않게 한다.
    private void AnimateMarch(float dt)
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

            _marchTime = Mathf.Wrap(_marchTime + moved.Length() * MarchRadiansPerUnit, 0f, Mathf.Tau);
            foreach (var member in _members)
            {
                var clock = _marchTime + member.Phase;
                var swing = Mathf.Sin(clock);

                // 걸음마다 한 번씩 몸이 뜬다 — 다리 주기의 두 배
                member.Body.Position = member.BodyBasePosition
                    + new Vector3(0f, Mathf.Abs(Mathf.Sin(clock)) * 0.012f, 0f);

                member.LegL.Rotation = new Vector3(swing * 0.45f, 0f, 0f);
                member.LegR.Rotation = new Vector3(-swing * 0.45f, 0f, 0f);

                // 팔은 다리와 반대로. 오른팔은 칼을 들고 있으니 덜 흔든다
                member.ArmL.Rotation = new Vector3(-swing * 0.34f, 0f, 0f);
                member.ArmR.Rotation = member.ArmRBaseRotation + new Vector3(swing * 0.16f, 0f, 0f);
            }
        }
        else if (_marchTime != 0f && !_attacking)
        {
            // 정지: 기준 자세로 복귀
            _marchTime = 0f;
            foreach (var member in _members)
            {
                member.Body.Position = member.BodyBasePosition;
                member.Body.Rotation = member.BodyBaseRotation;
                member.LegL.Rotation = Vector3.Zero;
                member.LegR.Rotation = Vector3.Zero;
                member.ArmL.Rotation = Vector3.Zero;
                member.ArmR.Rotation = member.ArmRBaseRotation;
            }
        }
    }

    // 공격 모션 타이밍. 젖힘은 길고 느리게, 내리침은 짧고 빠르게 — 대비가 힘을 만든다.
    private const float AttackRippleSeconds = 0.34f;  // 앞뒤 자리 차이 1당 지연
    private const float WindUpSeconds = 0.15f;
    private const float StrikeSeconds = 0.07f;
    private const float RecoverSeconds = 0.28f;

    /// <summary>
    /// 공격 모션: 앞줄부터 차례로 칼을 젖혔다 내리친다.
    /// 편대원이 동시에 같은 동작을 하면 아무리 크게 흔들어도 밋밋해지므로,
    /// 자리에 따라 시작을 늦추고 상체를 엇갈리게 튼다.
    /// </summary>
    public void PlayAttackMotion()
    {
        if (_moving || _attacking)
        {
            return;
        }

        _attacking = true;
        var forward = new Vector3(Mathf.Sin(Rotation.Y), 0f, Mathf.Cos(Rotation.Y));
        var origin = Position;
        var lastDelay = 0f;

        foreach (var member in _members)
        {
            lastDelay = Mathf.Max(lastDelay, member.AttackDelay);
            var tween = CreateTween();
            tween.TweenInterval(member.AttackDelay);

            // 1) 젖힘 — 칼을 뒤로 치켜들고 상체를 뒤로 젖히며 비튼다. 방패는 올려 막는다
            tween.Chain().TweenProperty(member.ArmR, "rotation:x",
                    member.ArmRBaseRotation.X - 1.15f, WindUpSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(member.ArmL, "rotation:x", -0.55f, WindUpSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X + 0.16f, WindUpSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "rotation:y",
                    member.BodyBaseRotation.Y + member.TwistSign * 0.26f, WindUpSeconds)
                .SetTrans(Tween.TransitionType.Sine);

            // 2) 내리침 — 젖힘의 절반도 안 되는 시간에 두 배 거리를 지난다
            tween.Chain().TweenProperty(member.ArmR, "rotation:x",
                    member.ArmRBaseRotation.X + 1.0f, StrikeSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X - 0.24f, StrikeSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(member.Body, "rotation:y",
                    member.BodyBaseRotation.Y, StrikeSeconds)
                .SetTrans(Tween.TransitionType.Quad);

            // 3) 복귀 — 반동으로 되돌아온다
            tween.Chain().TweenProperty(member.ArmR, "rotation:x",
                    member.ArmRBaseRotation.X, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(member.ArmL, "rotation:x", 0f, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
        }

        // 편대 전체는 선두가 칠 때 맞춰 한 번 쏠렸다 돌아온다
        var surge = CreateTween();
        surge.TweenInterval(WindUpSeconds * 0.85f);
        surge.Chain().TweenProperty(this, "position", origin + forward * 0.22f, StrikeSeconds + 0.03f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        surge.Chain().TweenProperty(this, "position", origin, RecoverSeconds)
            .SetTrans(Tween.TransitionType.Sine);
        // 마지막 편대원이 복귀를 끝낼 때까지 기다렸다 잠금을 푼다
        surge.Chain().TweenInterval(lastDelay);
        surge.Finished += () => _attacking = false;
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
                // 반투명 오버레이도 기본값은 그림자를 드리운다 — 지면 소품 위 그림자 어른거림 방지
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.95f, 0.90f, 0.70f, 0.75f),
                    EmissionEnabled = true,
                    Emission = new Color(0.55f, 0.48f, 0.28f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    // 지면 소품과 깊이를 다투지 않게 항상 위에 그린다(깜빡임 방지)
                    NoDepthTest = true,
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
            // 호버 육각이 타일에 그림자를 드리우면 낮은 소품(모래톱 등) 위가 어두워지며 반짝인다
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.92f, 0.55f, 0.28f),
                EmissionEnabled = true,
                Emission = new Color(0.6f, 0.52f, 0.25f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                // 잔교·모래톱 같은 낮은 소품과 깊이를 다투면 카메라 이동+호버 시 깜빡인다
                // — 깊이 테스트 없이 항상 위에 그린다
                NoDepthTest = true,
            },
        };
        _overlay.AddChild(_hover);
    }

    // 편대를 세우고 편대원마다 애니메이션 대상 부위를 이름으로 수집한다.
    // 부위 이름은 보병 공용 규약(tools/blender/infantry_common.py)을 따른다.
    private void BuildToken()
    {
        TroopFormation.Build(this, GD.Load<PackedScene>(TroopModel), TroopCount);

        var index = 0;
        foreach (var child in GetChildren())
        {
            if (child is not Node3D instance || instance.FindChild("body", true, false) is not Node3D body)
            {
                continue;
            }

            var armR = (Node3D)instance.FindChild("arm_r", true, false);
            _members.Add(new Member
            {
                Body = body,
                LegL = (Node3D)instance.FindChild("leg_l", true, false),
                LegR = (Node3D)instance.FindChild("leg_r", true, false),
                ArmL = (Node3D)instance.FindChild("arm_l", true, false),
                ArmR = armR,
                BodyBasePosition = body.Position,
                BodyBaseRotation = body.Rotation,
                ArmRBaseRotation = armR.Rotation,
                // 편대원끼리 발이 겹치지 않게 위상을 흩는다
                Phase = index * 0.9f,
                // 앞줄(+Z)일수록 먼저 친다. 뒤에서 계산해 채운다
                AttackDelay = instance.Position.Z,
                TwistSign = index % 2 == 0 ? 1f : -1f,
            });
            index++;
        }

        // 자리의 Z를 지연 시간으로 환산한다 — 선두 0에서 시작해 뒤로 갈수록 늦다
        var front = float.MinValue;
        foreach (var member in _members)
        {
            front = Mathf.Max(front, member.AttackDelay);
        }

        foreach (var member in _members)
        {
            member.AttackDelay = (front - member.AttackDelay) * AttackRippleSeconds;
        }

        _lastPosition = Position;
    }
}
