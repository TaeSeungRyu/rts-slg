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

    // 다리 궤적의 앞뒤 비대칭. 0이면 순수 사인이고 그때 가장 기계처럼 보인다.
    private const float SwingSkew = 0.45f;

    private MapView3D _view = null!;
    private HexMap _map = null!;
    private MovementService _movement = null!;
    private HexPathfinder _pathfinder = null!;
    private Unit _unit = null!;
    private Camera3D _camera = null!;
    private bool _moving;
    private bool _attacking;

    // 편대 검수용 임시 지정 — 병종 데이터(data/troop-types.json)가 생기면 그쪽에서 받는다.
    private static readonly string[] TroopModels =
    {
        "res://assets/models/troop-swordsman.glb",
        "res://assets/models/troop-cavalry.glb",
    };

    private const int TroopCount = 7;
    private int _troopIndex;

    /// <summary>모션 규약. 부위 노드 이름으로 판별한다 — 병종 데이터가 생기면 그쪽에서 받는다.</summary>
    private enum MotionKind { Infantry, Cavalry }

    // 프로시저럴 애니메이션 대상. 편대원마다 부위 노드와 기준 자세를 들고 있다.
    // 보병과 기병은 부위 구성이 다르므로 "같은 위상 / 반대 위상"으로 묶어 공통으로 다룬다.
    /// <summary>
    /// 흔들리는 다리 하나. 위상과 진폭을 부위마다 따로 준다 — 넷이 두 짝으로 딱 맞아
    /// 움직이면 태엽 장난감처럼 보인다.
    /// </summary>
    private sealed class SwingPart
    {
        public Node3D Node = null!;

        /// <summary>발굽·발. 다리와 반대로 꺾어 관절이 있는 것처럼 보이게 한다.</summary>
        public Node3D? Tip;

        public float Phase;
        public float Amplitude;
    }

    private sealed class Member
    {
        public Node3D Body = null!;
        public SwingPart[] Swings = System.Array.Empty<SwingPart>();

        /// <summary>다리와 반대로, 더 작게 흔들리는 부위(보병 왼팔).</summary>
        public Node3D? CounterSwing;

        /// <summary>몸통 위에서 한 박자 늦게 흔들리는 상체(기병 기수).</summary>
        public Node3D? Rider;

        public Node3D AttackArm = null!;
        public Node3D? ShieldArm;

        public Vector3 BodyBasePosition;
        public Vector3 BodyBaseRotation;
        public Vector3 RiderBaseRotation;
        public Vector3 AttackArmBaseRotation;
        public float Phase;

        /// <summary>공격 시작 지연 — 앞줄부터 차례로 친다.</summary>
        public float AttackDelay;

        /// <summary>상체를 트는 방향(±1) — 편대원끼리 엇갈리게 한다.</summary>
        public float TwistSign;
    }

    private readonly List<Member> _members = new();
    private MotionKind _motion;
    private Node3D _tokenRoot = null!;
    private CpuParticles3D? _dust;
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

    // 이동 모션: 진행 방향으로 회전 + 몸통 상하 흔들림 + 다리 교차 스윙.
    // 보병은 행군, 기병은 갤럽(대각 트롯) — 진폭과 주기만 다르고 구조는 같다.
    // 편대원마다 위상을 어긋나게 줘 발이 한꺼번에 떨어지지 않게 한다.
    private void AnimateMarch(float dt)
    {
        var moved = Position - _lastPosition;
        _lastPosition = Position;

        var cavalry = _motion == MotionKind.Cavalry;
        var stride = cavalry ? 16f : MarchRadiansPerUnit;
        var bob = cavalry ? 0.022f : 0.012f;
        var pitch = cavalry ? 0.09f : 0f;

        if (_dust is not null)
        {
            _dust.Emitting = _moving;
        }

        if (_moving)
        {
            if (moved.LengthSquared() > 0.000001f)
            {
                var targetYaw = Mathf.Atan2(moved.X, moved.Z);
                Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, targetYaw, 1f - Mathf.Exp(-14f * dt)), 0f);
            }

            _marchTime = Mathf.Wrap(_marchTime + moved.Length() * stride, 0f, Mathf.Tau);
            foreach (var member in _members)
            {
                var clock = _marchTime + member.Phase;
                var swing = Mathf.Sin(clock);

                // 걸음마다 한 번씩 몸이 뜬다 — 다리 주기의 두 배
                member.Body.Position = member.BodyBasePosition
                    + new Vector3(0f, Mathf.Abs(Mathf.Sin(clock)) * bob, 0f);
                // 기병은 도약할 때 몸통 앞뒤가 같이 들린다
                member.Body.Rotation = member.BodyBaseRotation + new Vector3(swing * pitch, 0f, 0f);

                foreach (var leg in member.Swings)
                {
                    // 순수 사인은 앞뒤로 오가는 시간이 같아 기계처럼 보인다.
                    // 위상을 자기 자신으로 흔들어 내딛는 쪽은 빠르게, 딛고 미는 쪽은 느리게 만든다.
                    var t = clock + leg.Phase;
                    var step = Mathf.Sin(t + SwingSkew * Mathf.Sin(t));
                    leg.Node.Rotation = new Vector3(step * leg.Amplitude, 0f, 0f);
                    if (leg.Tip is not null)
                    {
                        // 발굽은 반대로 꺾어 지면과 나란히 유지 — 무릎이 없어도 관절처럼 읽힌다
                        leg.Tip.Rotation = new Vector3(-step * leg.Amplitude * 0.7f, 0f, 0f);
                    }
                }

                // 보병 왼팔은 다리와 반대로. 오른팔은 무기를 들고 있으니 덜 흔든다
                if (member.CounterSwing is not null)
                {
                    member.CounterSwing.Rotation = new Vector3(-swing * 0.34f, 0f, 0f);
                }

                member.AttackArm.Rotation =
                    member.AttackArmBaseRotation + new Vector3(swing * 0.16f, 0f, 0f);

                // 기수는 말 몸통 위에서 반 박자 늦게 흔들린다 — 같이 굳어 있으면 인형처럼 보인다
                if (member.Rider is not null)
                {
                    member.Rider.Rotation = member.RiderBaseRotation
                        + new Vector3(Mathf.Sin(clock - 0.9f) * 0.07f, 0f, 0f);
                }
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
                foreach (var leg in member.Swings)
                {
                    leg.Node.Rotation = Vector3.Zero;
                    if (leg.Tip is not null)
                    {
                        leg.Tip.Rotation = Vector3.Zero;
                    }
                }

                if (member.CounterSwing is not null)
                {
                    member.CounterSwing.Rotation = Vector3.Zero;
                }

                if (member.Rider is not null)
                {
                    member.Rider.Rotation = member.RiderBaseRotation;
                }

                member.AttackArm.Rotation = member.AttackArmBaseRotation;
            }
        }
    }

    // 공격 모션 타이밍. 젖힘은 길고 느리게, 휘두름은 짧고 빠르게 — 대비가 힘을 만든다.
    private const float AttackRippleSeconds = 0.34f;  // 앞뒤 자리 차이 1당 지연
    private const float WindUpSeconds = 0.15f;
    private const float SwingSeconds = 0.07f;
    private const float ShieldPushSeconds = 0.13f;
    private const float RecoverSeconds = 0.28f;

    /// <summary>
    /// 공격 모션. 편대원이 동시에 같은 동작을 하면 아무리 크게 흔들어도 밋밋해지므로,
    /// 자리에 따라 시작을 늦추고 상체를 엇갈리게 튼다.
    /// 보병: 제자리에 선 채 칼을 휘두르고 방패로 민다.
    /// 기병: 앞으로 살짝 몰아 나가며 내리치고 물러 돌아온다(돌격).
    /// </summary>
    public void PlayAttackMotion()
    {
        if (_moving || _attacking)
        {
            return;
        }

        _attacking = true;
        if (_motion == MotionKind.Cavalry)
        {
            PlayCavalryCharge();
            return;
        }

        var lastDelay = 0f;

        foreach (var member in _members)
        {
            lastDelay = Mathf.Max(lastDelay, member.AttackDelay);
            var shield = member.ShieldArm;
            var tween = CreateTween();
            tween.TweenInterval(member.AttackDelay);

            // 1) 젖힘 — 칼을 머리 위로 치켜들고 상체를 뒤로 젖힌다. 방패는 몸쪽으로 당겨 둔다
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X - 1.55f, WindUpSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            if (shield is not null)
            {
                tween.Parallel().TweenProperty(shield, "rotation:x", -0.45f, WindUpSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X + 0.26f, WindUpSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "rotation:y",
                    member.BodyBaseRotation.Y + member.TwistSign * 0.14f, WindUpSeconds)
                .SetTrans(Tween.TransitionType.Sine);

            // 2) 내리침 — 젖힘의 절반도 안 되는 시간에 두 배 거리를 지난다.
            //    상체가 앞으로 꺾이며 칼을 위에서 아래로 끌고 내려온다
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X + 1.40f, SwingSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X - 0.36f, SwingSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(member.Body, "rotation:y",
                    member.BodyBaseRotation.Y - member.TwistSign * 0.06f, SwingSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

            // 3) 방패 밀기 — 칼을 거둬들이면서 반대쪽 방패를 앞으로 내지른다.
            //    상체가 방패 쪽으로 다시 돌아가 체중을 싣는다
            if (shield is not null)
            {
                tween.Chain().TweenProperty(shield, "rotation:x", 0.85f, ShieldPushSeconds)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            }

            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X + 0.35f, ShieldPushSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "rotation:y",
                    member.BodyBaseRotation.Y + member.TwistSign * 0.14f, ShieldPushSeconds)
                .SetTrans(Tween.TransitionType.Quad);
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X - 0.10f, ShieldPushSeconds)
                .SetTrans(Tween.TransitionType.Sine);

            // 4) 복귀 — 반동으로 되돌아온다
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            if (shield is not null)
            {
                tween.Parallel().TweenProperty(shield, "rotation:x", 0f, RecoverSeconds)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            }
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "rotation:y",
                    member.BodyBaseRotation.Y, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
        }

        // 마지막 편대원이 복귀를 끝낼 때까지 기다렸다 잠금을 푼다
        var clock = CreateTween();
        clock.TweenInterval(lastDelay + WindUpSeconds + SwingSeconds + ShieldPushSeconds + RecoverSeconds);
        clock.Finished += () => _attacking = false;
    }

    // 기병 돌격 타이밍. 몰아 나가는 동안 젖혔다가 최전방에서 내리친다.
    private const float ChargeOutSeconds = 0.22f;
    private const float ChargeBackSeconds = 0.34f;
    private const float ChargeDistance = 0.13f;

    // 돌격: 부대 전체가 앞으로 몰아 나가며 편대원마다 칼을 젖혔다 내리치고,
    // 말이 앞다리를 들며 멈춘 뒤 물러 돌아온다. 먼지는 몰아 나가는 동안 뿜는다.
    private void PlayCavalryCharge()
    {
        var forward = new Vector3(Mathf.Sin(Rotation.Y), 0f, Mathf.Cos(Rotation.Y));
        var origin = Position;
        var lastDelay = 0f;

        if (_dust is not null)
        {
            _dust.Emitting = true;
        }

        foreach (var member in _members)
        {
            lastDelay = Mathf.Max(lastDelay, member.AttackDelay);
            var tween = CreateTween();
            tween.TweenInterval(member.AttackDelay);

            // 1) 젖힘 — 몰아 나가는 동안 칼을 치켜들고 기수가 앞으로 숙인다
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X - 1.45f, ChargeOutSeconds * 0.8f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            if (member.Rider is not null)
            {
                tween.Parallel().TweenProperty(member.Rider, "rotation:x",
                        member.RiderBaseRotation.X - 0.30f, ChargeOutSeconds * 0.8f)
                    .SetTrans(Tween.TransitionType.Sine);
            }

            // 2) 내리침 — 최전방 도달에 맞춰 짧고 빠르게
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X + 1.25f, SwingSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            if (member.Rider is not null)
            {
                tween.Parallel().TweenProperty(member.Rider, "rotation:x",
                        member.RiderBaseRotation.X + 0.22f, SwingSeconds)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }

            // 3) 말이 앞다리를 들며 급정지 — 몸통이 뒤로 들린다
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X + 0.30f, SwingSeconds + 0.05f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

            // 4) 복귀
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X, ChargeBackSeconds)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X, ChargeBackSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            if (member.Rider is not null)
            {
                tween.Parallel().TweenProperty(member.Rider, "rotation:x",
                        member.RiderBaseRotation.X, ChargeBackSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }
        }

        // 부대 전체: 몰아 나감 → 잠깐 버팀 → 물러 돌아옴
        var surge = CreateTween();
        surge.TweenProperty(this, "position", origin + forward * ChargeDistance, ChargeOutSeconds)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        surge.TweenCallback(Callable.From(() =>
        {
            if (_dust is not null)
            {
                _dust.Emitting = false;
            }
        }));
        surge.TweenInterval(0.10f);
        surge.Chain().TweenProperty(this, "position", origin, ChargeBackSeconds)
            .SetTrans(Tween.TransitionType.Sine);
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

        // T: 병종 전환 — 검수용. 병종 데이터가 생기면 편성 UI가 대신한다
        if (@event is InputEventKey { Pressed: true, Keycode: Key.T } && !_moving && !_attacking)
        {
            _troopIndex = (_troopIndex + 1) % TroopModels.Length;
            BuildToken();
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
    // 부위 이름 규약: 보병은 tools/blender/infantry_common.py, 기병은 make_troop_cavalry.py.
    // 다시 불러도 되도록 만들었다 — 병종 전환(T)이 이 함수를 재실행한다.
    private void BuildToken()
    {
        _members.Clear();
        _dust = null;
        _tokenRoot?.QueueFree();

        _tokenRoot = new Node3D();
        AddChild(_tokenRoot);
        TroopFormation.Build(_tokenRoot, GD.Load<PackedScene>(TroopModels[_troopIndex]), TroopCount);

        var index = 0;
        foreach (var child in _tokenRoot.GetChildren())
        {
            if (child is not Node3D instance || instance.FindChild("body", true, false) is not Node3D body)
            {
                continue;
            }

            // leg_fl이 있으면 기병 규약이다
            var cavalry = instance.FindChild("leg_fl", true, false) is Node3D;
            _motion = cavalry ? MotionKind.Cavalry : MotionKind.Infantry;

            Node3D Part(string name) => (Node3D)instance.FindChild(name, true, false);

            var rider = cavalry ? Part("rider") : null;
            var attackArm = Part(cavalry ? "rider_arm_r" : "arm_r");
            var member = new Member
            {
                Body = body,
                Rider = rider,
                AttackArm = attackArm,
                ShieldArm = cavalry ? null : Part("arm_l"),
                CounterSwing = cavalry ? null : Part("arm_l"),
                Swings = cavalry ? CavalryLegs(Part) : InfantryLegs(Part),
                BodyBasePosition = body.Position,
                BodyBaseRotation = body.Rotation,
                RiderBaseRotation = rider?.Rotation ?? Vector3.Zero,
                AttackArmBaseRotation = attackArm.Rotation,
                // 편대원끼리 발이 겹치지 않게 위상을 흩는다
                Phase = index * 0.9f,
                // 앞줄(+Z)일수록 먼저 친다. 뒤에서 계산해 채운다
                AttackDelay = instance.Position.Z,
                TwistSign = index % 2 == 0 ? 1f : -1f,
            };

            _members.Add(member);
            index++;
        }

        if (_motion == MotionKind.Cavalry)
        {
            _dust = BuildHoofDust();
            _tokenRoot.AddChild(_dust);
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
        MapView3D.TuneImportedMeshes(_tokenRoot);
    }

    private static SwingPart[] InfantryLegs(System.Func<string, Node3D> part) => new[]
    {
        new SwingPart { Node = part("leg_l"), Tip = part("foot_l"), Phase = 0f, Amplitude = 0.45f },
        new SwingPart { Node = part("leg_r"), Tip = part("foot_r"), Phase = Mathf.Pi, Amplitude = 0.45f },
    };

    // 갤럽은 네 다리가 두 짝으로 딱 맞는 게 아니라 뒷다리부터 차례로 구른다.
    // 위상을 조금씩 어긋나게 주고, 미는 뒷다리를 앞다리보다 크게 흔든다.
    private static SwingPart[] CavalryLegs(System.Func<string, Node3D> part) => new[]
    {
        new SwingPart { Node = part("leg_bl"), Tip = part("hoof_bl"), Phase = 0.00f * Mathf.Tau, Amplitude = 0.50f },
        new SwingPart { Node = part("leg_br"), Tip = part("hoof_br"), Phase = 0.16f * Mathf.Tau, Amplitude = 0.50f },
        new SwingPart { Node = part("leg_fl"), Tip = part("hoof_fl"), Phase = 0.53f * Mathf.Tau, Amplitude = 0.36f },
        new SwingPart { Node = part("leg_fr"), Tip = part("hoof_fr"), Phase = 0.69f * Mathf.Tau, Amplitude = 0.36f },
    };

    // 말발굽이 이는 먼지. 이동 중에만 뿜고 멈추면 끈다.
    // LocalCoords를 끄면 먼지가 월드에 남아 지나온 자리에 꼬리가 생긴다.
    private static CpuParticles3D BuildHoofDust()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.78f, 0.70f, 0.56f, 0f));
        gradient.AddPoint(0.18f, new Color(0.76f, 0.68f, 0.54f, 0.42f));
        gradient.SetColor(1, new Color(0.80f, 0.74f, 0.62f, 0f));

        return new CpuParticles3D
        {
            Position = new Vector3(0f, 0.01f, 0f),
            Amount = 16,
            Lifetime = 0.8f,
            Emitting = false,
            LocalCoords = false,
            Mesh = new SphereMesh
            {
                Radius = 0.032f,
                Height = 0.048f,
                RadialSegments = 6,
                Rings = 3,
                Material = new StandardMaterial3D
                {
                    VertexColorUseAsAlbedo = true,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
            },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(0.15f, 0.005f, 0.13f),
            Direction = new Vector3(0f, 1f, 0f),
            Spread = 32f,
            InitialVelocityMin = 0.09f,
            InitialVelocityMax = 0.20f,
            Gravity = new Vector3(0f, -0.06f, 0f),
            DampingMin = 0.4f,
            DampingMax = 0.7f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.5f,
            ColorRamp = gradient,
        };
    }
}
