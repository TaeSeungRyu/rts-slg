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
    // Solo: 편대 없이 항상 1개로 표현(대선 규칙).
    private static readonly (string File, bool Solo)[] TroopModels =
    {
        ("res://assets/models/troop-swordsman.glb", false),
        ("res://assets/models/troop-cavalry.glb", false),
        ("res://assets/models/troop-archer.glb", false),
        ("res://assets/models/troop-thunder-cart.glb", false),
        ("res://assets/models/troop-catapult.glb", false),
        ("res://assets/models/troop-siege-tower.glb", false),
        ("res://assets/models/troop-war-elephant.glb", false),
        ("res://assets/models/troop-small-boat.glb", false),
        ("res://assets/models/troop-medium-ship.glb", false),
        ("res://assets/models/troop-large-ship.glb", true),
        ("res://assets/models/troop-pikeman.glb", false),
        ("res://assets/models/troop-nanman.glb", false),
        ("res://assets/models/troop-shieldbearer.glb", false),
        ("res://assets/models/troop-wudang.glb", false),
        ("res://assets/models/troop-cataphract.glb", false),
    };

    private const int TroopCount = 7;

    // 시작 병종 = 목록의 마지막(가장 최근 작업물). 새 병종 검수 때 T 연타가 필요 없다
    private int _troopIndex = TroopModels.Length - 1;

    /// <summary>모션 규약. 부위 노드 이름으로 판별한다 — 병종 데이터가 생기면 그쪽에서 받는다.</summary>
    private enum MotionKind { Infantry, Cavalry, Archer, Siege, Elephant, Ship }

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

        /// <summary>손에 쥔 화살 — 발사 순간 숨기고 발사체로 잇는다(궁병).</summary>
        public Node3D? Arrow;

        /// <summary>바퀴 — 이동 거리에 비례해 굴린다(공성).</summary>
        public Node3D[] Wheels = System.Array.Empty<Node3D>();

        /// <summary>돛 — 이동 중 돛대 축으로 흔들리고 펄럭인다(선박). 돛대가 여럿이면 여럿.</summary>
        public Node3D[] Sails = System.Array.Empty<Node3D>();
        public Vector3[] SailBaseRotations = System.Array.Empty<Vector3>();

        /// <summary>갑판 궁병들의 손 화살 — 발사 순간 각자 발사체로 잇는다(대선).</summary>
        public Node3D[] DeckArrows = System.Array.Empty<Node3D>();

        /// <summary>말뚝 내지르기용 기준 위치(공성 AttackArm은 회전이 아니라 위치를 움직인다).</summary>
        public Vector3 AttackArmBasePosition;

        public Vector3 BodyBasePosition;
        public Vector3 BodyBaseRotation;
        public Vector3 RiderBaseRotation;
        public Vector3 AttackArmBaseRotation;
        public float Phase;

        /// <summary>공격 시작 지연 — 편대원마다 제각각 흩어져 있다.</summary>
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

    /// <summary>돌격 중 전진·후퇴 구간 — 이동이 아니어도 다리를 굴린다.</summary>
    private bool _chargeMoving;

    /// <summary>공성 중에서도 던지는 쪽(투석기) — 말뚝 대신 팔을 젖혀 돌을 날린다.</summary>
    private bool _siegeThrower;

    /// <summary>공성 중에서도 쏘는 쪽(공성탑) — 탑 위 궁병이 활을 쏜다.</summary>
    private bool _siegeArcher;

    /// <summary>보병 중 창을 든 쪽(극병 등) — 휘두르기 대신 찌른다.</summary>
    private bool _pikeInfantry;

    // 하이라이트·경로 오버레이는 유닛과 함께 움직이면 안 되므로 형제 노드에 담는다.
    private Node3D _overlay = null!;
    private MeshInstance3D _hover = null!;
    private readonly List<MeshInstance3D> _pathMarkers = new();
    private HexCoord? _hoverCoord;

    private Color _factionColor = new(0.75f, 0.15f, 0.15f);

    public void Init(HexMap map, MapView3D view, Camera3D camera, Unit unit, Color factionColor)
    {
        _factionColor = factionColor;
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
        var siege = _motion == MotionKind.Siege;
        var elephant = _motion == MotionKind.Elephant;
        var ship = _motion == MotionKind.Ship;
        var stride = cavalry ? 16f : elephant ? 9f : ship ? 6f : MarchRadiansPerUnit;
        var bob = cavalry ? 0.022f : elephant ? 0.010f : siege ? 0.003f : ship ? 0.004f : 0.012f;
        var pitch = cavalry ? 0.09f : elephant ? 0.045f : ship ? 0.030f : 0f;

        var animMove = _moving || _chargeMoving;
        if (_dust is not null)
        {
            _dust.Emitting = animMove;
        }

        if (animMove)
        {
            // 돌격 중에는 회전하지 않는다 — 후퇴 구간에서 방향을 뒤집어버린다
            if (_moving && moved.LengthSquared() > 0.000001f)
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

                // 바퀴는 이동 거리에 비례해 구른다. 리셋 없음 — 회전 대칭이라 어디서 멈춰도 된다
                foreach (var wheel in member.Wheels)
                {
                    wheel.Rotation = new Vector3(
                        wheel.Rotation.X + moved.Length() / SiegeWheelRadius, 0f, 0f);
                }

                // 선박: 선체가 옆으로도 흔들리고(롤), 돛들이 돛대 축으로 흔들리며 펄럭인다.
                // 돛마다 위상을 어긋내 두 돛이 한 몸처럼 움직이지 않게 한다
                if (ship)
                {
                    member.Body.Rotation = member.BodyBaseRotation
                        + new Vector3(swing * pitch, 0f, Mathf.Sin(clock * 0.6f) * 0.05f);
                    for (var k = 0; k < member.Sails.Length; k++)
                    {
                        var sc = clock + k * 0.9f;
                        member.Sails[k].Rotation = member.SailBaseRotations[k] + new Vector3(
                            Mathf.Sin(sc * 1.4f) * 0.05f, Mathf.Sin(sc * 0.7f) * 0.16f, 0f);
                    }

                    continue;
                }

                // 돌격 중에는 팔·기수를 공격 트윈이 쥐고 있다 — 매 프레임 덮어쓰면 안 된다.
                // 공성의 AttackArm은 말뚝이라 행군 흔들기 대상이 아니다
                if (_attacking || siege)
                {
                    continue;
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
    private const float AttackScatterSeconds = 0.55f;  // 편대원 시작 지연이 흩어지는 폭
    private const float WindUpSeconds = 0.15f;
    private const float SwingSeconds = 0.07f;
    private const float MeleeStepSeconds = 0.16f;      // 보병 근접의 들어가는 한 발
    private const float RecoverSeconds = 0.28f;

    /// <summary>
    /// 공격 모션. 편대원이 동시에 같은 동작을 하면 아무리 크게 흔들어도 밋밋해지므로,
    /// 시작 시점을 제각각 흩고 상체를 엇갈리게 튼다.
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

        if (_motion == MotionKind.Archer)
        {
            PlayArcherVolley();
            return;
        }

        if (_motion == MotionKind.Elephant)
        {
            PlayElephantRam();
            return;
        }

        if (_motion == MotionKind.Ship)
        {
            PlayShipRam();
            return;
        }

        if (_pikeInfantry)
        {
            PlayPikeThrust();
            return;
        }

        if (_motion == MotionKind.Siege)
        {
            if (_siegeArcher)
            {
                PlayTowerVolley();
            }
            else if (_siegeThrower)
            {
                PlayCatapultVolley();
            }
            else
            {
                PlaySiegeRam();
            }

            return;
        }

        // 보병 근접(도검병·남만병 등): 한 발 들어가서 팔만 휘두르고 물러난다.
        // 상체 회전은 쓰지 않는다 — 뒤로 젖히면 배가 나오고, 앞으로 꺾어도 어색하다
        // (2026-08-07 사용자 확인).
        var lastDelay = 0f;

        foreach (var member in _members)
        {
            lastDelay = Mathf.Max(lastDelay, member.AttackDelay);
            var shield = member.ShieldArm;
            var tween = CreateTween();
            tween.TweenInterval(member.AttackDelay);

            // 1) 전진 + 젖힘 — 한 발 들어가면서 칼을 치켜든다. 방패는 앞을 가린다
            tween.Chain().TweenProperty(member.Body, "position:z",
                    member.BodyBasePosition.Z + 0.06f, MeleeStepSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X - 1.55f, MeleeStepSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            if (shield is not null)
            {
                tween.Parallel().TweenProperty(shield, "rotation:x", -0.35f, MeleeStepSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }

            // 2) 내리침 — 팔만 움직인다
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X + 1.40f, SwingSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

            // 3) 잠깐 멈춘 뒤 물러나며 복귀
            tween.Chain().TweenInterval(0.08f);
            tween.Chain().TweenProperty(member.Body, "position:z",
                    member.BodyBasePosition.Z, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            if (shield is not null)
            {
                tween.Parallel().TweenProperty(shield, "rotation:x", 0f, RecoverSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }
        }

        // 마지막 편대원이 복귀를 끝낼 때까지 기다렸다 잠금을 푼다
        var clock = CreateTween();
        clock.TweenInterval(lastDelay + MeleeStepSeconds + SwingSeconds + 0.08f + RecoverSeconds);
        clock.Finished += () => _attacking = false;
    }

    // 기병 돌격 타이밍.
    private const float ChargeOutSeconds = 0.42f;
    private const float ChargeBackSeconds = 0.46f;
    private const float ChargeDistance = 0.26f;

    // 돌격 3단계(사용자 정의): ① 말이 다리를 구르며 앞으로 간다
    // ② 멈춘 말 위에서 기수들이 제각각 친다 — 말은 움직이지 않는다 ③ 물러 돌아온다.
    private void PlayCavalryCharge()
    {
        var forward = new Vector3(Mathf.Sin(Rotation.Y), 0f, Mathf.Cos(Rotation.Y));
        var origin = Position;
        var lastDelay = 0f;

        // 기수 공격 — 전진이 끝난 뒤 제각각 시작한다. 말(Body)은 건드리지 않는다
        foreach (var member in _members)
        {
            lastDelay = Mathf.Max(lastDelay, member.AttackDelay);
            var tween = CreateTween();
            tween.TweenInterval(ChargeOutSeconds + member.AttackDelay);

            // 젖힘 — 칼을 치켜들고 기수가 뒤로 젖힌다
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X - 1.45f, WindUpSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            if (member.Rider is not null)
            {
                tween.Parallel().TweenProperty(member.Rider, "rotation:x",
                        member.RiderBaseRotation.X - 0.24f, WindUpSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }

            // 내리침 — 기수가 앞으로 쏟아지며 짧고 빠르게
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X + 1.25f, SwingSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            if (member.Rider is not null)
            {
                tween.Parallel().TweenProperty(member.Rider, "rotation:x",
                        member.RiderBaseRotation.X + 0.28f, SwingSeconds)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            }

            // 복귀
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            if (member.Rider is not null)
            {
                tween.Parallel().TweenProperty(member.Rider, "rotation:x",
                        member.RiderBaseRotation.X, RecoverSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }
        }

        // 기수 전원이 복귀를 끝낼 때까지가 공격 구간이다
        var attackWindow = lastDelay + WindUpSeconds + SwingSeconds + RecoverSeconds;

        // 부대 전체: 다리를 구르며 전진 → 말은 서고 기수만 공격 → 다리를 구르며 복귀.
        // _chargeMoving이 켜진 동안 AnimateMarch가 이동 거리에 맞춰 다리를 굴린다.
        _chargeMoving = true;
        var surge = CreateTween();
        surge.TweenProperty(this, "position", origin + forward * ChargeDistance, ChargeOutSeconds)
            .SetTrans(Tween.TransitionType.Sine);
        surge.TweenCallback(Callable.From(() =>
        {
            _chargeMoving = false;
            ResetStancePose();
        }));
        surge.TweenInterval(attackWindow);
        surge.Chain().TweenCallback(Callable.From(() => _chargeMoving = true));
        surge.Chain().TweenProperty(this, "position", origin, ChargeBackSeconds)
            .SetTrans(Tween.TransitionType.Sine);
        surge.Finished += () =>
        {
            _chargeMoving = false;
            ResetStancePose();
            _attacking = false;
        };
    }

    // 궁병 사격 타이밍. 당김은 길게, 조준에서 멈칫, 놓는 것은 한순간.
    private const float DrawSeconds = 0.30f;
    private const float AimHoldSeconds = 0.18f;
    private const float ReleaseSeconds = 0.05f;
    private const float ArrowFlightSeconds = 0.6f;

    // 사거리 2(design-unit.md range_unit). 병종 데이터가 생기면 그쪽에서 받는다.
    private const float ArrowRangeTiles = 2f;

    // 사격: 활을 앞으로 뻗어 올리고 시위 손을 귀 뒤까지 당겨 조준했다가 놓는다.
    // 놓는 순간 손의 화살이 사라지고 발사체가 같은 자리에서 날아간다 — 이 연결이 활맛의 핵심.
    // 시작이 제각각이라 일제사보다는 연달아 쏘는 그림이 된다.
    private void PlayArcherVolley()
    {
        var lastDelay = 0f;

        for (var i = 0; i < _members.Count; i++)
        {
            var member = _members[i];
            lastDelay = Mathf.Max(lastDelay, member.AttackDelay);
            var bowArm = member.ShieldArm;
            // 낙점을 좌우로 흩는다 — 전원이 한 점에 꽂히면 가짜처럼 보인다
            var scatter = (i * 0.618034f % 1f - 0.5f) * 0.5f;

            var tween = CreateTween();
            tween.TweenInterval(member.AttackDelay);

            // 1) 당김 — 활을 수평 너머까지 뻗어 올리고, 시위 손이 귀 뒤까지 온다.
            //    상체가 크게 뒤로 젖혀지며 옆으로 튼다(사수 자세)
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X - 1.35f, DrawSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            if (bowArm is not null)
            {
                tween.Parallel().TweenProperty(bowArm, "rotation:x", -1.45f, DrawSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X + 0.22f, DrawSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "rotation:y",
                    member.BodyBaseRotation.Y + member.TwistSign * 0.20f, DrawSeconds)
                .SetTrans(Tween.TransitionType.Sine);

            // 2) 조준 — 당긴 채 멈칫. 이 정지가 긴장을 만든다
            tween.Chain().TweenInterval(AimHoldSeconds);

            // 3) 발사 — 손의 화살을 숨기고 같은 자리에서 발사체를 쏜다
            tween.Chain().TweenCallback(Callable.From(() => LooseArrow(member, scatter, ArrowRangeTiles)));
            tween.Parallel().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X - 0.55f, ReleaseSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            if (bowArm is not null)
            {
                tween.Parallel().TweenProperty(bowArm, "rotation:x", -1.30f, ReleaseSeconds)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            }
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X + 0.08f, ReleaseSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

            // 4) 복귀 — 내려오는 길에 화살을 다시 메긴다(다시 보이게)
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            if (bowArm is not null)
            {
                tween.Parallel().TweenProperty(bowArm, "rotation:x", 0f, RecoverSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "rotation:y",
                    member.BodyBaseRotation.Y, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                if (member.Arrow is not null)
                {
                    member.Arrow.Visible = true;
                }
            }));
        }

        var clock = CreateTween();
        clock.TweenInterval(lastDelay + DrawSeconds + AimHoldSeconds + ReleaseSeconds + RecoverSeconds);
        clock.Finished += () => _attacking = false;
    }

    // 발사 순간: 손의 화살을 숨기고, 그 자리에서 사거리만큼 앞의 지면으로 발사체를 날린다.
    private void LooseArrow(Member member, float scatter, float rangeTiles)
    {
        var from = member.Arrow?.GlobalPosition
            ?? member.Body.GlobalPosition + Vector3.Up * 0.10f;
        if (member.Arrow is not null)
        {
            member.Arrow.Visible = false;
        }

        var forward = new Vector3(Mathf.Sin(Rotation.Y), 0f, Mathf.Cos(Rotation.Y));
        var lateral = new Vector3(forward.Z, 0f, -forward.X);
        var to = new Vector3(from.X, Position.Y, from.Z)
            + forward * rangeTiles + lateral * scatter;
        ProjectileView.SpawnArrow(_overlay, from, to, ArrowFlightSeconds);
    }

    // 공성탑 사거리(design-unit.md range_unit). 병종 데이터가 생기면 그쪽에서 받는다.
    private const float TowerRangeTiles = 2f;

    // 탑 위 사격: 탑(Body)은 미동도 없고, 위의 궁병(Rider)만 당기고 조준하고 놓는다.
    private void PlayTowerVolley()
    {
        var lastDelay = 0f;

        for (var i = 0; i < _members.Count; i++)
        {
            var member = _members[i];
            lastDelay = Mathf.Max(lastDelay, member.AttackDelay);
            var bowArm = member.ShieldArm;
            var scatter = (i * 0.618034f % 1f - 0.5f) * 0.6f;

            var tween = CreateTween();
            tween.TweenInterval(member.AttackDelay);

            // 1) 당김
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X - 1.05f, DrawSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            if (bowArm is not null)
            {
                tween.Parallel().TweenProperty(bowArm, "rotation:x", -1.30f, DrawSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }
            if (member.Rider is not null)
            {
                tween.Parallel().TweenProperty(member.Rider, "rotation:x",
                        member.RiderBaseRotation.X + 0.16f, DrawSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }

            // 2) 조준
            tween.Chain().TweenInterval(AimHoldSeconds);

            // 3) 발사
            tween.Chain().TweenCallback(Callable.From(() => LooseArrow(member, scatter, TowerRangeTiles)));
            tween.Parallel().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X - 0.40f, ReleaseSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

            // 4) 복귀 — 내려오는 길에 화살을 다시 메긴다
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            if (bowArm is not null)
            {
                // 탑 궁병 활팔의 대기 자세(-42도). 모델 노드 회전과 같아야 제자리로 돌아온다
                tween.Parallel().TweenProperty(bowArm, "rotation:x", -0.733f, RecoverSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }
            if (member.Rider is not null)
            {
                tween.Parallel().TweenProperty(member.Rider, "rotation:x",
                        member.RiderBaseRotation.X, RecoverSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                if (member.Arrow is not null)
                {
                    member.Arrow.Visible = true;
                }
            }));
        }

        var clock = CreateTween();
        clock.TweenInterval(lastDelay + DrawSeconds + AimHoldSeconds + ReleaseSeconds + RecoverSeconds);
        clock.Finished += () => _attacking = false;
    }

    // 공성 말뚝 타이밍. 뒤로 무겁게 당겼다가 한순간에 박는다.
    private const float SiegeWheelRadius = 0.055f;
    private const float RamPullSeconds = 0.30f;
    private const float RamStrikeSeconds = 0.06f;
    private const float RamPitchRadians = 0.28f;

    // 말뚝 박기: 수레는 서고 말뚝이 축 방향(앞·위 16도)으로 당겨졌다 박힌다.
    // 박히는 순간 수레가 반동으로 살짝 밀렸다 돌아온다.
    private void PlaySiegeRam()
    {
        var lastDelay = 0f;
        var thrust = new Vector3(0f, Mathf.Sin(RamPitchRadians), Mathf.Cos(RamPitchRadians));

        foreach (var member in _members)
        {
            lastDelay = Mathf.Max(lastDelay, member.AttackDelay);
            var tween = CreateTween();
            tween.TweenInterval(member.AttackDelay);

            // 1) 당김 — 말뚝을 뒤로 무겁게 뺀다
            tween.Chain().TweenProperty(member.AttackArm, "position",
                    member.AttackArmBasePosition - thrust * 0.06f, RamPullSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

            // 2) 박음 — 한순간에 앞으로
            tween.Chain().TweenProperty(member.AttackArm, "position",
                    member.AttackArmBasePosition + thrust * 0.16f, RamStrikeSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            // 반동 — 수레가 살짝 밀린다
            tween.Parallel().TweenProperty(member.Body, "position:z",
                    member.BodyBasePosition.Z - 0.014f, RamStrikeSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

            // 3) 복귀
            tween.Chain().TweenProperty(member.AttackArm, "position",
                    member.AttackArmBasePosition, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "position:z",
                    member.BodyBasePosition.Z, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
        }

        var clock = CreateTween();
        clock.TweenInterval(lastDelay + RamPullSeconds + RamStrikeSeconds + RecoverSeconds);
        clock.Finished += () => _attacking = false;
    }

    // 투석기 타이밍. 감아 젖히는 것은 무겁게, 팔이 튕겨 오르는 것은 한순간.
    private const float CatapultWindSeconds = 0.38f;
    private const float CatapultLooseSeconds = 0.11f;
    private const float StoneFlightSeconds = 0.95f;
    private const float CatapultRangeTiles = 2f;

    // 투석: 팔을 뒤로 감아 젖혔다가 튕겨 올리고, 정점에서 돌이 떨어져 나가 포물선으로 날아간다.
    private void PlayCatapultVolley()
    {
        var lastDelay = 0f;

        for (var i = 0; i < _members.Count; i++)
        {
            var member = _members[i];
            lastDelay = Mathf.Max(lastDelay, member.AttackDelay);
            var scatter = (i * 0.618034f % 1f - 0.5f) * 0.7f;

            var tween = CreateTween();
            tween.TweenInterval(member.AttackDelay);

            // 1) 감아 젖힘 — 팔이 뒤로 더 눕고 수레가 살짝 뒤로 눌린다
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X - 0.30f, CatapultWindSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X - 0.03f, CatapultWindSeconds)
                .SetTrans(Tween.TransitionType.Sine);

            // 2) 발사 — 팔이 앞으로 튕겨 오르고, 끝나는 순간 돌이 떨어져 나간다
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X + 1.55f, CatapultLooseSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X + 0.05f, CatapultLooseSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Chain().TweenCallback(Callable.From(() => LooseStone(member, scatter)));

            // 3) 복귀 — 팔이 천천히 내려오고 돌을 다시 얹는다
            tween.Chain().TweenInterval(0.12f);
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X, RecoverSeconds + 0.15f)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                if (member.Arrow is not null)
                {
                    member.Arrow.Visible = true;
                }
            }));
        }

        var clock = CreateTween();
        clock.TweenInterval(lastDelay + CatapultWindSeconds + CatapultLooseSeconds
            + 0.12f + RecoverSeconds + 0.15f);
        clock.Finished += () => _attacking = false;
    }

    // 발사 순간: 바구니의 돌을 숨기고, 그 자리에서 사거리만큼 앞의 지면으로 돌을 날린다.
    private void LooseStone(Member member, float scatter)
    {
        var from = member.Arrow?.GlobalPosition
            ?? member.Body.GlobalPosition + Vector3.Up * 0.2f;
        if (member.Arrow is not null)
        {
            member.Arrow.Visible = false;
        }

        var forward = new Vector3(Mathf.Sin(Rotation.Y), 0f, Mathf.Cos(Rotation.Y));
        var lateral = new Vector3(forward.Z, 0f, -forward.X);
        var to = new Vector3(from.X, Position.Y, from.Z)
            + forward * CatapultRangeTiles + lateral * scatter;
        ProjectileView.SpawnStone(_overlay, from, to, StoneFlightSeconds);
    }

    // 들이받기 타이밍. 무겁게 다가가서 코를 치켜들었다 내리찍는다.
    private const float ElephantOutSeconds = 0.50f;
    private const float ElephantBackSeconds = 0.55f;
    private const float ElephantDistance = 0.20f;
    private const float TrunkSlamSeconds = 0.09f;

    // 들이받기 3단계: 다리를 구르며 육중하게 전진 → 멈춰 서서 코를 치켜들었다 내리찍음
    // (몸이 앞으로 쏠린다) → 물러 돌아옴. 좌우 병사는 아무것도 하지 않는다.
    private void PlayElephantRam()
    {
        var forward = new Vector3(Mathf.Sin(Rotation.Y), 0f, Mathf.Cos(Rotation.Y));
        var origin = Position;
        var lastDelay = 0f;

        foreach (var member in _members)
        {
            lastDelay = Mathf.Max(lastDelay, member.AttackDelay);
            var tween = CreateTween();
            tween.TweenInterval(ElephantOutSeconds + member.AttackDelay);

            // 1) 코 치켜듦
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X - 1.15f, WindUpSeconds + 0.06f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

            // 2) 내리찍음 — 코가 한순간에 떨어지고 몸이 앞으로 쏠린다
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X + 0.40f, TrunkSlamSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X + 0.15f, TrunkSlamSeconds + 0.04f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

            // 3) 복귀
            tween.Chain().TweenProperty(member.AttackArm, "rotation:x",
                    member.AttackArmBaseRotation.X, RecoverSeconds + 0.10f)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
        }

        var slamWindow = lastDelay + WindUpSeconds + 0.06f + TrunkSlamSeconds + RecoverSeconds + 0.10f;

        _chargeMoving = true;
        var surge = CreateTween();
        surge.TweenProperty(this, "position", origin + forward * ElephantDistance, ElephantOutSeconds)
            .SetTrans(Tween.TransitionType.Sine);
        surge.TweenCallback(Callable.From(() =>
        {
            _chargeMoving = false;
            ResetStancePose();
        }));
        surge.TweenInterval(slamWindow);
        surge.Chain().TweenCallback(Callable.From(() => _chargeMoving = true));
        surge.Chain().TweenProperty(this, "position", origin, ElephantBackSeconds)
            .SetTrans(Tween.TransitionType.Sine);
        surge.Finished += () =>
        {
            _chargeMoving = false;
            ResetStancePose();
            _attacking = false;
        };
    }

    // 선박 들이받기 타이밍. 물살을 가르며 다가가 뱃머리로 찍는다.
    private const float ShipOutSeconds = 0.45f;
    private const float ShipBackSeconds = 0.50f;
    private const float ShipDistance = 0.16f;

    // 소선 사거리(design-unit.md range_unit). 병종 데이터가 생기면 그쪽에서 받는다.
    private const float ShipRangeTiles = 1f;

    // 선박 공격: 물보라를 일으키며 전진 → 뱃머리를 찍는 순간 갑판에서 화살이 날아간다
    // → 물러 돌아온다.
    private void PlayShipRam()
    {
        var forward = new Vector3(Mathf.Sin(Rotation.Y), 0f, Mathf.Cos(Rotation.Y));
        var origin = Position;
        var lastDelay = 0f;

        for (var i = 0; i < _members.Count; i++)
        {
            var member = _members[i];
            lastDelay = Mathf.Max(lastDelay, member.AttackDelay);
            var scatter = (i * 0.618034f % 1f - 0.5f) * 0.4f;

            var tween = CreateTween();
            tween.TweenInterval(ShipOutSeconds + member.AttackDelay);

            tween.Chain().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X + 0.14f, 0.10f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Chain().TweenCallback(Callable.From(() => LooseDeckArrows(member, scatter)));
            tween.Chain().TweenProperty(member.Body, "rotation:x",
                    member.BodyBaseRotation.X, RecoverSeconds + 0.10f)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                foreach (var arrow in member.DeckArrows)
                {
                    arrow.Visible = true;
                }
            }));
        }

        var slamWindow = lastDelay + 0.10f + RecoverSeconds + 0.10f;

        _chargeMoving = true;
        var surge = CreateTween();
        surge.TweenProperty(this, "position", origin + forward * ShipDistance, ShipOutSeconds)
            .SetTrans(Tween.TransitionType.Sine);
        surge.TweenCallback(Callable.From(() =>
        {
            _chargeMoving = false;
            ResetStancePose();
        }));
        surge.TweenInterval(slamWindow);
        surge.Chain().TweenCallback(Callable.From(() => _chargeMoving = true));
        surge.Chain().TweenProperty(this, "position", origin, ShipBackSeconds)
            .SetTrans(Tween.TransitionType.Sine);
        surge.Finished += () =>
        {
            _chargeMoving = false;
            ResetStancePose();
            _attacking = false;
        };
    }

    // 발사 순간: 갑판 궁병이 있으면 각자의 손 화살을 숨기고 그 자리에서 쏜다.
    // 없으면(소선·중선) 갑판 한가운데서 한 발.
    private void LooseDeckArrows(Member member, float scatter)
    {
        var forward = new Vector3(Mathf.Sin(Rotation.Y), 0f, Mathf.Cos(Rotation.Y));
        var lateral = new Vector3(forward.Z, 0f, -forward.X);

        if (member.DeckArrows.Length == 0)
        {
            var from = member.Body.GlobalPosition + Vector3.Up * 0.16f + forward * 0.08f;
            var to = new Vector3(from.X, Position.Y, from.Z)
                + forward * ShipRangeTiles + lateral * scatter;
            ProjectileView.SpawnArrow(_overlay, from, to, 0.45f);
            return;
        }

        for (var k = 0; k < member.DeckArrows.Length; k++)
        {
            var arrow = member.DeckArrows[k];
            var from = arrow.GlobalPosition;
            arrow.Visible = false;
            var spread = scatter + (k - (member.DeckArrows.Length - 1) * 0.5f) * 0.22f;
            var to = new Vector3(from.X, Position.Y, from.Z)
                + forward * ShipRangeTiles + lateral * spread;
            ProjectileView.SpawnArrow(_overlay, from, to, 0.45f);
        }
    }

    // 뱃머리가 가르는 물보라. 이동 중에만 뿜는다 — 말발굽 먼지와 같은 자리(_dust)를 쓴다.
    private static CpuParticles3D BuildBowSpray(float bowOffset)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.85f, 0.93f, 0.97f, 0f));
        gradient.AddPoint(0.15f, new Color(0.82f, 0.92f, 0.96f, 0.55f));
        gradient.SetColor(1, new Color(0.88f, 0.95f, 0.98f, 0f));

        return new CpuParticles3D
        {
            Position = new Vector3(0f, 0.015f, bowOffset),
            Amount = 18,
            Lifetime = 0.55f,
            Emitting = false,
            LocalCoords = false,
            Mesh = new SphereMesh
            {
                Radius = 0.02f,
                Height = 0.03f,
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
            EmissionBoxExtents = new Vector3(0.06f, 0.004f, 0.03f),
            Direction = new Vector3(0f, 0.6f, 0.8f),
            Spread = 40f,
            InitialVelocityMin = 0.10f,
            InitialVelocityMax = 0.22f,
            Gravity = new Vector3(0f, -0.35f, 0f),
            ScaleAmountMin = 0.4f,
            ScaleAmountMax = 1.1f,
            ColorRamp = gradient,
        };
    }

    // 찌르기 타이밍. 창은 평상시부터 수평으로 겨눠져 있다(모델 자세) — 팔을 내리는
    // 단계가 아예 없다. 뒤로 당겼다가 크고 빠른 직선 피스톤 한 번.
    private const float PikePullSeconds = 0.18f;
    private const float PikeThrustSeconds = 0.08f;

    // 찌르기: 세워 든 창을 수평으로 겨눴다가 몸을 실어 앞으로 내지른다(극병).
    private void PlayPikeThrust()
    {
        var lastDelay = 0f;

        foreach (var member in _members)
        {
            lastDelay = Mathf.Max(lastDelay, member.AttackDelay);
            var tween = CreateTween();
            tween.TweenInterval(member.AttackDelay);

            // 1) 당김 — 창이 제 축을 따라 뒤로 빠지고 상체가 옆으로 튼다. 회전 없음
            tween.Chain().TweenProperty(member.AttackArm, "position:z",
                    member.AttackArmBasePosition.Z - 0.055f, PikePullSeconds)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(member.Body, "rotation:y",
                    member.BodyBaseRotation.Y + member.TwistSign * 0.18f, PikePullSeconds)
                .SetTrans(Tween.TransitionType.Sine);

            // 2) 피스톤 — 창이 제 축을 따라 크게 직선으로 뻗는다
            tween.Chain().TweenProperty(member.AttackArm, "position:z",
                    member.AttackArmBasePosition.Z + 0.145f, PikeThrustSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(member.Body, "position:z",
                    member.BodyBasePosition.Z + 0.055f, PikeThrustSeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(member.Body, "rotation:y",
                    member.BodyBaseRotation.Y - member.TwistSign * 0.06f, PikeThrustSeconds)
                .SetTrans(Tween.TransitionType.Quad);

            // 3) 잠깐 꽂아 둔다 — 찌른 창이 바로 튀어나오면 가볍게 보인다
            tween.Chain().TweenInterval(0.10f);

            // 4) 복귀
            tween.Chain().TweenProperty(member.AttackArm, "position:z",
                    member.AttackArmBasePosition.Z, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "position:z",
                    member.BodyBasePosition.Z, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(member.Body, "rotation:y",
                    member.BodyBaseRotation.Y, RecoverSeconds)
                .SetTrans(Tween.TransitionType.Sine);
        }

        var clock = CreateTween();
        clock.TweenInterval(lastDelay + PikePullSeconds + PikeThrustSeconds
            + 0.10f + RecoverSeconds);
        clock.Finished += () => _attacking = false;
    }

    // 다리·몸통만 기준 자세로 되돌린다. 팔·기수는 공격 트윈이 쥐고 있으므로 건드리지 않는다.
    private void ResetStancePose()
    {
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

            for (var k = 0; k < member.Sails.Length; k++)
            {
                member.Sails[k].Rotation = member.SailBaseRotations[k];
            }
        }
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
        var (modelFile, solo) = TroopModels[_troopIndex];
        TroopFormation.Build(_tokenRoot, GD.Load<PackedScene>(modelFile), solo ? 1 : TroopCount);

        var index = 0;
        foreach (var child in _tokenRoot.GetChildren())
        {
            if (child is not Node3D instance || instance.FindChild("body", true, false) is not Node3D body)
            {
                continue;
            }

            // trunk=상병 / sail=선박 / leg_fl=기병 / ram·arm_basket_base·tower_archer=공성 /
            // bow_grip=궁병 규약. 상병도 leg_fl을 쓰므로 trunk를 먼저 본다
            var elephant = instance.FindChild("trunk", true, false) is Node3D;
            var ship = !elephant && instance.FindChild("sail", true, false) is Node3D;
            var cavalry = !elephant && instance.FindChild("leg_fl", true, false) is Node3D;
            var ram = !cavalry && instance.FindChild("ram", true, false) is Node3D;
            _siegeThrower = !cavalry && instance.FindChild("arm_basket_base", true, false) is Node3D;
            _siegeArcher = !cavalry && instance.FindChild("tower_archer", true, false) is Node3D;
            var siege = ram || _siegeThrower || _siegeArcher;
            var archer = !cavalry && !siege && !elephant && !ship
                && instance.FindChild("bow_grip", true, false) is Node3D;
            _pikeInfantry = !cavalry && !siege && !elephant && !ship && !archer
                && instance.FindChild("pike", true, false) is Node3D;
            _motion = elephant ? MotionKind.Elephant
                : ship ? MotionKind.Ship
                : cavalry ? MotionKind.Cavalry
                : siege ? MotionKind.Siege
                : archer ? MotionKind.Archer
                : MotionKind.Infantry;

            // 규약 판별이 어긋나면 없는 부위를 찾게 된다 — 조용한 NRE 대신 어떤 이름이 없는지 말해준다
            Node3D Part(string name) => instance.FindChild(name, true, false) as Node3D
                ?? throw new System.InvalidOperationException(
                    $"부위 노드 없음: {name} — {TroopModels[_troopIndex].File}의 규약 판별을 확인할 것");

            Node3D[] FoundParts(params string[] names) => names
                .Select(n => instance.FindChild(n, true, false) as Node3D)
                .Where(n => n is not null)
                .Cast<Node3D>()
                .ToArray();

            var rider = cavalry ? Part("rider") : _siegeArcher ? Part("tower_archer") : null;
            var attackArm = Part(elephant ? "trunk"
                : ship ? "sail"
                : cavalry ? "rider_arm_r"
                : ram ? "ram"
                : _siegeThrower ? "arm"
                : _siegeArcher ? "ta_arm_r"
                : "arm_r");
            var member = new Member
            {
                Body = body,
                Rider = rider,
                AttackArm = attackArm,
                AttackArmBasePosition = attackArm.Position,
                ShieldArm = _siegeArcher ? Part("ta_arm_l")
                    : cavalry || siege || elephant || ship ? null
                    : Part("arm_l"),
                CounterSwing = cavalry || siege || elephant || ship ? null : Part("arm_l"),
                Arrow = archer ? Part("arrow")
                    : _siegeThrower ? Part("stone")
                    : _siegeArcher ? Part("ta_arrow")
                    : null,
                Wheels = siege
                    ? FoundParts("wheel_l", "wheel_r", "wheel_fl", "wheel_fr", "wheel_bl", "wheel_br")
                    : System.Array.Empty<Node3D>(),
                Sails = ship ? FoundParts("sail", "sail2", "sail3", "sail4") : System.Array.Empty<Node3D>(),
                DeckArrows = ship
                    ? FoundParts("da0_arrow", "da1_arrow", "da2_arrow", "da3_arrow",
                        "da4_arrow", "da5_arrow", "da6_arrow", "da7_arrow")
                    : System.Array.Empty<Node3D>(),
                Swings = elephant ? ElephantLegs(Part)
                    : ship ? System.Array.Empty<SwingPart>()
                    : cavalry ? CavalryLegs(Part)
                    : _siegeArcher ? System.Array.Empty<SwingPart>()
                    : siege ? SiegeLegs(Part)
                    : InfantryLegs(Part),
                BodyBasePosition = body.Position,
                BodyBaseRotation = body.Rotation,
                RiderBaseRotation = rider?.Rotation ?? Vector3.Zero,
                AttackArmBaseRotation = attackArm.Rotation,
                // 편대원끼리 발이 겹치지 않게 위상을 흩는다
                Phase = index * 0.9f,
                // 공격 시작을 제각각 흩는다. 황금비 간격이라 순번이 이어져도
                // 자리 순서(파도)로도, 규칙적인 박자로도 읽히지 않는다
                AttackDelay = index * 0.618034f % 1f * AttackScatterSeconds,
                TwistSign = index % 2 == 0 ? 1f : -1f,
            };

            member.SailBaseRotations = member.Sails.Select(n => n.Rotation).ToArray();
            _members.Add(member);
            index++;
        }

        if (_motion is MotionKind.Cavalry or MotionKind.Elephant)
        {
            _dust = BuildHoofDust();
            _tokenRoot.AddChild(_dust);
        }
        else if (_motion == MotionKind.Ship)
        {
            _dust = BuildBowSpray(solo ? 0.36f : 0.13f);
            _tokenRoot.AddChild(_dust);
        }

        _lastPosition = Position;
        FactionColorView.Apply(_tokenRoot, _factionColor);
        MapView3D.TuneImportedMeshes(_tokenRoot);
    }

    private static SwingPart[] InfantryLegs(System.Func<string, Node3D> part) => new[]
    {
        new SwingPart { Node = part("leg_l"), Tip = part("foot_l"), Phase = 0f, Amplitude = 0.45f },
        new SwingPart { Node = part("leg_r"), Tip = part("foot_r"), Phase = Mathf.Pi, Amplitude = 0.45f },
    };

    // 끄는 병사 둘 — 사람 걸음이므로 진폭은 보병과 같고, 둘의 위상만 어긋낸다.
    private static SwingPart[] SiegeLegs(System.Func<string, Node3D> part) => new[]
    {
        new SwingPart { Node = part("crew0_leg_l"), Tip = part("crew0_foot_l"), Phase = 0f, Amplitude = 0.45f },
        new SwingPart { Node = part("crew0_leg_r"), Tip = part("crew0_foot_r"), Phase = Mathf.Pi, Amplitude = 0.45f },
        new SwingPart { Node = part("crew1_leg_l"), Tip = part("crew1_foot_l"), Phase = 0.7f, Amplitude = 0.45f },
        new SwingPart { Node = part("crew1_leg_r"), Tip = part("crew1_foot_r"), Phase = 0.7f + Mathf.Pi, Amplitude = 0.45f },
    };

    // 코끼리 걸음: 같은 쪽 다리가 거의 붙어 움직이는 측대보(lateral walk). 무겁고 느리다.
    // 옆에서 걷는 꼬마 병사 다리도 같은 시계를 탄다 — 몸이 작으니 성큼성큼 따라온다.
    private static SwingPart[] ElephantLegs(System.Func<string, Node3D> part) => new[]
    {
        new SwingPart { Node = part("leg_fl"), Phase = 0.00f * Mathf.Tau, Amplitude = 0.26f },
        new SwingPart { Node = part("leg_bl"), Phase = 0.14f * Mathf.Tau, Amplitude = 0.28f },
        new SwingPart { Node = part("leg_fr"), Phase = 0.50f * Mathf.Tau, Amplitude = 0.26f },
        new SwingPart { Node = part("leg_br"), Phase = 0.64f * Mathf.Tau, Amplitude = 0.28f },
        new SwingPart { Node = part("walker0_leg_l"), Tip = part("walker0_foot_l"), Phase = 0f, Amplitude = 0.52f },
        new SwingPart { Node = part("walker0_leg_r"), Tip = part("walker0_foot_r"), Phase = Mathf.Pi, Amplitude = 0.52f },
        new SwingPart { Node = part("walker1_leg_l"), Tip = part("walker1_foot_l"), Phase = 0.8f, Amplitude = 0.52f },
        new SwingPart { Node = part("walker1_leg_r"), Tip = part("walker1_foot_r"), Phase = 0.8f + Mathf.Pi, Amplitude = 0.52f },
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
