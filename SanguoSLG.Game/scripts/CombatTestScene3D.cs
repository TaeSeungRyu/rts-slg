using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 이동→전투 통합 검증 하베스트(doc/test/combat-movement-cases.md). 부대가 목적지로 이동하다 조우해
/// Core <see cref="AdvanceOrchestrator"/>가 한 "진행"을 계산하면, 재생 규칙(한 칸 이동 1초 → 공격 모션
/// 1초)대로 토큰을 옮기고 병종별 공격 모션을 재생한 뒤, 전투 결과를 표에 한 행씩 쌓는다. 각 유닛에
/// 패시브 1·액티브 1을 붙이고, 일부 케이스는 계략을 예약해 발동·지속 상태·강제 후퇴를 보여준다.
/// 규칙·수치는 Core 소유.
/// </summary>
public partial class CombatTestScene3D : Node3D
{
    private static readonly Color Blue = new(0.24f, 0.44f, 0.86f);
    private static readonly Color Red = new(0.82f, 0.22f, 0.18f);
    private const int MaxQ = 16;
    private const int MaxR = 8;

    private static readonly CombatContext MeleeCtx = new(MeleeEngagement: true, IncomingMelee: true, InField: true);

    private static readonly Dictionary<string, int> ModelIndex = new()
    {
        ["swordsman"] = 0, ["cavalry"] = 1, ["archer"] = 2, ["thunder_cart"] = 3,
        ["catapult"] = 4, ["siege_tower"] = 5, ["war_elephant"] = 6, ["small_boat"] = 7,
        ["medium_ship"] = 8, ["large_ship"] = 9, ["turtleship"] = 17,
    };

    private sealed record CaseDef(string Title, string Note, System.Func<CombatUnit[]> Build);

    private MapView3D _view = null!;
    private CameraController3D _camera = null!;
    private AdvanceOrchestrator _orchestrator = null!;

    private IReadOnlyDictionary<string, TroopTemplate> _templates = null!;
    private IReadOnlyDictionary<string, ActiveSkill> _actives = null!;
    private IReadOnlyDictionary<string, PassiveSkill> _passives = null!;
    private IReadOnlyDictionary<string, Stratagem> _strats = null!;

    private CaseDef[] _cases = System.Array.Empty<CaseDef>();
    private int _caseIndex;
    private int _round;
    private bool _aggregate; // 부대가 많으면(대량 전투) 표를 유닛별 대신 진영 집계로
    private int _initialA;
    private int _initialE;
    private List<CombatUnit> _units = new();
    private readonly List<int> _orderedIds = new();
    private readonly Dictionary<int, UnitController3D> _tokens = new();
    private readonly Dictionary<int, Label3D> _troopLabels = new();
    private readonly Dictionary<int, Label3D> _statusLabels = new();
    private readonly Dictionary<int, int> _tokenModel = new();
    private readonly Dictionary<int, HexCoord> _tokenHex = new();
    private readonly List<Node3D> _spawned = new();

    // 재생 규칙(2026-08-11 사용자 정의 — 실제 게임에도 적용): 한 칸 이동 1초, 공격 모션 1초.
    private Godot.Timer _beatTimer = null!;
    private bool _animating;
    private Queue<System.Action> _beats = new();
    private AdvanceTurn? _pending;

    private Button _stepButton = null!;
    private Button _caseButton = null!;
    private Label _titleLabel = null!;
    private Label _noteLabel = null!;
    private GridContainer _table = null!;

    public void Build(MapView3D view, CameraController3D camera, string dataDirectory)
    {
        _view = view;
        _camera = camera;

        _templates = new TroopTypeLoader().LoadFromDirectory(dataDirectory).ToDictionary(t => t.Code);
        _actives = new ActiveSkillLoader().LoadFromDirectory(dataDirectory).ToDictionary(a => a.Code);
        _passives = new PassiveSkillLoader().LoadFromDirectory(dataDirectory).ToDictionary(p => p.Code);
        _strats = new StratagemLoader().LoadFromDirectory(dataDirectory).ToDictionary(s => s.Code);

        var map = new HexMap(0, MaxQ, 0, MaxR);
        _orchestrator = new AdvanceOrchestrator(
            new MovementSimulator(new PassabilityMap(map, [], [])),
            new CombatPhaseResolver(new BattleResolver(60), woundedPercent: 70),
            woundedPercent: 70,
            terrainAt: _ => TerrainType.Plains);

        _cases = BuildCases();
        BuildHud();

        _beatTimer = new Godot.Timer { WaitTime = 0.5, OneShot = false };
        AddChild(_beatTimer);
        _beatTimer.Timeout += OnBeat;

        LoadCase(0);

        // 헤드리스 자동 검증(예외용): 애니메이션 없이 즉시 진행.
        if (OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).Contains("--combattestauto"))
        {
            var rounds = 0;
            var timer = new Godot.Timer { WaitTime = 0.2, Autostart = true };
            AddChild(timer);
            timer.Timeout += () =>
            {
                if (Ended() || rounds >= 12)
                {
                    GD.Print($"[combattestauto] case {_caseIndex} after {rounds}: " +
                        string.Join(" ", _units.Select(u => $"{Tag(u)}={u.Pool.Active}")));
                    if (_caseIndex + 1 < _cases.Length) { LoadCase(_caseIndex + 1); rounds = 0; }
                    else { GD.Print("[combattestauto] all cases done"); timer.Stop(); }
                    return;
                }

                rounds++;
                BeginTurn();
                FinalizeTurn();
            };
        }
    }

    // ── 케이스 ──

    private CombatUnit Unit(int id, int owner, HexCoord pos, string templateCode, HexCoord? target, UnitMode mode,
        string passiveCode, string activeCode, int might = 60, int intellect = 60, int troops = 10000,
        string? stratagemCode = null, int stratagemTarget = 0)
    {
        var (atk, df) = PassiveBucketEvaluator.Evaluate(new[] { (_passives[passiveCode], 3) }, MeleeCtx);
        var stats = CombatStatsBuilder.BuildField(_templates[templateCode], AptitudeGrade.A, 0, TerrainType.River,
            troops, atkBonusPercent: atk, dfBonusPercent: df);
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos, 2, 2, 1,
            MovementDomain.Land, mode, target, id);
        _tokenModel[id] = ModelIndex.GetValueOrDefault(templateCode, 0);
        var state = UnitCombatState.Create(intellect, vanguardActive: _actives[activeCode]);
        if (stratagemCode is not null)
        {
            state = state.ReserveStratagem(_strats[stratagemCode], new UnitId(stratagemTarget));
        }

        return new CombatUnit(field, stats, new TroopPool(troops, 0), state, might, intellect, MaxTroops: troops);
    }

    private CaseDef[] BuildCases() => new[]
    {
        new CaseDef("진격 조우 → 소모전",
            "A1(공격)이 동진해 정지 방어자 E2를 추격·정지 → 교전. A1=맹공+무쌍(발동 라운드 큰 데미지), E2=견수+정비(부상 회복).",
            () => new[]
            {
                Unit(1, 1, new HexCoord(0, 1), "swordsman", new HexCoord(10, 1), UnitMode.Attack, "fierce_assault", "peerless", might: 80),
                Unit(2, 2, new HexCoord(7, 1), "swordsman", null, UnitMode.Advance, "steadfast_guard", "regroup", intellect: 80),
            }),
        new CaseDef("전진 직행(무전투)",
            "A1(전진)은 길목의 E2(행군)를 무시하고 목표로 직행 → 조우 없이 도달. 표에 '없음'이 이어진다.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(0, 1), "swordsman", new HexCoord(10, 1), UnitMode.Advance, "fierce_assault", "peerless"),
                Unit(2, 2, new HexCoord(6, 0), "swordsman", null, UnitMode.March, "steadfast_guard", "iron_wall"),
            }),
        new CaseDef("정면 조우 교전",
            "A1·E2가 서로 목표로 마주 진격 → 가운데서 정지 → 대칭 소모. 둘 다 맹공+무쌍.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(0, 1), "swordsman", new HexCoord(10, 1), UnitMode.Attack, "fierce_assault", "peerless", might: 80),
                Unit(2, 2, new HexCoord(10, 1), "swordsman", new HexCoord(0, 1), UnitMode.Attack, "fierce_assault", "peerless", might: 80),
            }),
        new CaseDef("다대일 협격(이동 포위)",
            "A1·A2가 양쪽에서 중앙의 E4(상병+정비)로 진격·포위. 상병 반격은 주대상 A1 100%/A2 60%로 갈려 A1이 먼저 무너진다. 상병은 정비로 버틴다.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(0, 1), "swordsman", new HexCoord(4, 1), UnitMode.Attack, "fierce_assault", "peerless"),
                Unit(2, 1, new HexCoord(10, 1), "swordsman", new HexCoord(6, 1), UnitMode.Attack, "fierce_assault", "peerless"),
                Unit(4, 2, new HexCoord(5, 1), "war_elephant", null, UnitMode.Advance, "steadfast_guard", "regroup", intellect: 80),
            }),
        new CaseDef("화계 — 지속 피해",
            "A1이 인접(사거리 1)한 E2에 화계 예약 → 2진행 뒤 발동, 이후 진행마다 화상으로 병력이 깎인다(표 '지속 −n', 상태 '화상n'). 둘 다 행군이라 교전은 없다.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(0, 1), "archer", null, UnitMode.March, "steadfast_guard", "regroup", intellect: 90, stratagemCode: "fire_plot", stratagemTarget: 2),
                Unit(2, 2, new HexCoord(1, 1), "swordsman", null, UnitMode.March, "steadfast_guard", "iron_wall"),
            }),
        new CaseDef("혼란 — 행동불가",
            "A1(공격)이 인접 E2에 혼란 예약 → 발동하면 E2가 3진행 동안 공격·이동 불가(E2 '준 0'·상태 '행동불가'). A1은 계속 친다.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(0, 1), "swordsman", new HexCoord(9, 1), UnitMode.Attack, "fierce_assault", "peerless", might: 80, intellect: 90, stratagemCode: "confound", stratagemTarget: 2),
                Unit(2, 2, new HexCoord(3, 1), "swordsman", new HexCoord(0, 1), UnitMode.Attack, "steadfast_guard", "iron_wall"),
            }),
        new CaseDef("교란 — 강제 후퇴",
            "A1이 E2에 교란 예약 → 발동 시 즉발 5% + E2가 시전자 반대쪽으로 밀려난다(토큰이 뒤로 물러남).",
            () => new[]
            {
                Unit(1, 1, new HexCoord(0, 1), "cavalry", null, UnitMode.Advance, "fierce_assault", "peerless", intellect: 90, stratagemCode: "rout", stratagemTarget: 2),
                Unit(2, 2, new HexCoord(2, 1), "swordsman", null, UnitMode.March, "steadfast_guard", "iron_wall"),
            }),
        new CaseDef("폭파 — 광역",
            "A1이 인접(사거리 1)한 E2에 폭파 예약 → 발동 시 대상 E2와 인접 적 E3이 함께 6% 피해(둘 다 '잔여' 감소). 모두 행군이라 교전은 없다.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(0, 1), "catapult", null, UnitMode.March, "steadfast_guard", "regroup", intellect: 90, stratagemCode: "detonate", stratagemTarget: 2),
                Unit(2, 2, new HexCoord(1, 1), "swordsman", null, UnitMode.March, "steadfast_guard", "iron_wall"),
                Unit(3, 2, new HexCoord(2, 1), "swordsman", null, UnitMode.March, "steadfast_guard", "iron_wall"),
            }),
        new CaseDef("대량 전투 — 양군 충돌",
            "아군 A·적군 E가 2랭크 5행(각 20기)으로 마주 진격해 전선에서 맞붙는다. 전열이 갈려나가면(전멸) 토큰이 사라지고, 뒤 랭크가 빈자리로 밀려든다. 표는 진영 집계(병력 합·생존).",
            BigBattle),
    };

    // 양 진영 2랭크 × 5행 대군. 각 유닛은 반대편 같은 행으로 진격(공격모드). 앞 랭크는 맹공·무쌍,
    // 뒤 랭크는 견수·정비로 오래 버틴다 — 전선 교대가 보이도록.
    private CombatUnit[] BigBattle()
    {
        var list = new List<CombatUnit>();
        var id = 1;
        for (var r = 2; r <= 6; r++)
        {
            list.Add(Unit(id++, 1, new HexCoord(1, r), "swordsman", new HexCoord(15, r), UnitMode.Attack, "fierce_assault", "peerless", might: 78));
            list.Add(Unit(id++, 1, new HexCoord(0, r), "swordsman", new HexCoord(15, r), UnitMode.Attack, "steadfast_guard", "regroup", intellect: 78));
            list.Add(Unit(id++, 2, new HexCoord(14, r), "swordsman", new HexCoord(0, r), UnitMode.Attack, "fierce_assault", "peerless", might: 78));
            list.Add(Unit(id++, 2, new HexCoord(15, r), "swordsman", new HexCoord(0, r), UnitMode.Attack, "steadfast_guard", "regroup", intellect: 78));
        }

        return list.ToArray();
    }

    // ── 진행 (애니메이션: 이동 1초/칸 → 공격 1초) ──

    private void OnStep()
    {
        if (_animating || Ended())
        {
            return;
        }

        BeginTurn();
        _animating = true;
        _stepButton.Disabled = true;
        _caseButton.Disabled = true;

        if (_beats.Count == 0)
        {
            FinishAnimation();
        }
        else
        {
            _beatTimer.Start();
        }
    }

    // 한 진행을 계산하고 재생 비트(이동 틱마다 1개 + 전투가 있으면 공격 1개)를 큐에 쌓는다.
    private void BeginTurn()
    {
        _round++;
        _pending = _orchestrator.Run(_units);

        _beats = new Queue<System.Action>();
        // 실제로 위치가 바뀌는 틱만 이동 비트로 넣는다(정지/교전 스냅샷은 건너뛴다).
        var running = new Dictionary<int, HexCoord>(_tokenHex);
        foreach (var tick in _pending.Movement.Ticks)
        {
            var moves = tick.Units.Any(fu => running.GetValueOrDefault(fu.Id.Value, fu.Position) != fu.Position);
            if (!moves)
            {
                continue;
            }

            var snapshot = tick;
            _beats.Enqueue(() => MoveTokens(snapshot));
            foreach (var fu in tick.Units)
            {
                running[fu.Id.Value] = fu.Position;
            }
        }

        // 이동 시뮬 밖에서 위치가 바뀐 부대(교란 강제 후퇴 등)를 마지막에 정렬한다.
        if (_pending.Units.Any(u => running.GetValueOrDefault(u.Id.Value, u.Field.Position) != u.Field.Position))
        {
            _beats.Enqueue(SettleTokens);
        }

        if (_pending.Combat is not null)
        {
            _beats.Enqueue(PlayAttacks);
        }
    }

    // 이동 시뮬이 잡지 못한 위치 변화(교란 후퇴)를 토큰에 반영한다.
    private void SettleTokens()
    {
        foreach (var u in _pending!.Units)
        {
            if (_tokens.TryGetValue(u.Id.Value, out var ctrl)
                && _tokenHex.GetValueOrDefault(u.Id.Value, u.Field.Position) != u.Field.Position)
            {
                ctrl.DisplayStepTo(u.Field.Position, 0.5f);
                _tokenHex[u.Id.Value] = u.Field.Position;
            }
        }
    }

    private void OnBeat()
    {
        if (_beats.Count > 0)
        {
            _beats.Dequeue().Invoke();
        }
        else
        {
            _beatTimer.Stop();
            FinishAnimation();
        }
    }

    private void MoveTokens(MovementTick tick)
    {
        foreach (var fu in tick.Units)
        {
            if (!_tokens.TryGetValue(fu.Id.Value, out var ctrl)
                || _tokenHex.GetValueOrDefault(fu.Id.Value, fu.Position) == fu.Position)
            {
                continue; // 제자리면 이동 애니메이션을 걸지 않는다(공격 모션 리셋 방지)
            }

            ctrl.DisplayStepTo(fu.Position, 0.5f);
            _tokenHex[fu.Id.Value] = fu.Position;
            var foe = tick.Units.FirstOrDefault(o => o.Owner != fu.Owner);
            if (foe is not null)
            {
                ctrl.FaceToward(_view.HexToWorld(foe.Position));
            }
        }
    }

    private void PlayAttacks()
    {
        var combat = _pending!.Combat!;
        var units = _pending.Units;
        foreach (var u in units)
        {
            if (u.Pool.Active <= 0 || !_tokens.TryGetValue(u.Id.Value, out var ctrl))
            {
                continue;
            }

            // 이 교전에서 실제로 공격/피격에 관여한 부대만 공격 모션.
            if (!combat.DamageDealt.ContainsKey(u.Id) && !combat.DamageTaken.ContainsKey(u.Id))
            {
                continue;
            }

            var foe = units.FirstOrDefault(o => o.Field.Owner != u.Field.Owner && o.Pool.Active > 0);
            if (foe is not null)
            {
                ctrl.FaceToward(_view.HexToWorld(foe.Field.Position));
            }

            ctrl.PlayAttackMotion();
        }
    }

    private void FinishAnimation()
    {
        FinalizeTurn();
        _animating = false;
        _stepButton.Disabled = Ended();
        _caseButton.Disabled = false;
    }

    // 결과 확정: 병력 반영, 라벨 갱신, 표에 한 행 추가.
    private void FinalizeTurn()
    {
        var turn = _pending!;

        // 결과에서 사라진 부대 = 이번 진행에 전멸 → 토큰을 없앤다(영혼 상승 연출은 후속, design-effect SoulRise).
        var survivors = turn.Units.Select(u => u.Id.Value).ToHashSet();
        foreach (var id in _units.Select(u => u.Id.Value).Where(id => !survivors.Contains(id)).ToList())
        {
            DespawnToken(id);
        }

        _units = turn.Units.ToList();

        foreach (var u in _units)
        {
            RefreshLabel(u);
        }

        AddResultRow(turn);
        _pending = null;
    }

    // 전멸 부대 소멸: 토큰과 라벨을 제거한다. TODO(design-effect SoulRise): 제거 전 소멸 지점에
    // 영혼이 땅에서 솟아오르는 연출을 1회 재생.
    private void DespawnToken(int id)
    {
        if (_tokens.TryGetValue(id, out var ctrl))
        {
            _spawned.Remove(ctrl); // 케이스 전환 시 이중 해제 방지
            ctrl.QueueFree();
            _tokens.Remove(id);
        }

        _troopLabels.Remove(id);
        _statusLabels.Remove(id);
        _tokenHex.Remove(id);
    }

    private bool Ended()
        => _units.Where(u => u.Pool.Active > 0).Select(u => u.Field.Owner.Value).Distinct().Count() < 2;

    private void RefreshLabel(CombatUnit u)
    {
        var alive = u.Pool.Active > 0;
        _troopLabels[u.Id.Value].Text = alive ? $"{u.Pool.Active}/{u.MaxTroops}" : "전멸";
        _troopLabels[u.Id.Value].Modulate = alive ? new Color(0.97f, 0.96f, 0.92f) : new Color(0.9f, 0.4f, 0.35f);
        _statusLabels[u.Id.Value].Text = alive ? StatusTags(u) : "";
    }

    // 부대에 걸린 지속 상태를 짧은 태그로(토큰 아래 표시).
    private static string StatusTags(CombatUnit u) => string.Join(" ", u.State.Statuses.Select(s => s.Kind switch
    {
        StatusKind.Burn => $"화상{s.Remaining}",
        StatusKind.Poison => $"독{s.Remaining}",
        StatusKind.AttackDown => "공↓",
        StatusKind.RangedDown => "원↓",
        StatusKind.Nullify => "무효",
        StatusKind.Daze => $"행동불가{s.Remaining}",
        _ => "",
    }));

    private static string Tag(CombatUnit u) => (u.Field.Owner.Value == 1 ? "A" : "E") + u.Id.Value;

    // ── 셋업/토큰 ──

    private void LoadCase(int index)
    {
        _caseIndex = index;
        _round = 0;
        _animating = false;
        _beatTimer.Stop();
        _pending = null;
        var def = _cases[index];

        foreach (var node in _spawned)
        {
            node.QueueFree();
        }

        _spawned.Clear();
        _tokens.Clear();
        _troopLabels.Clear();
        _statusLabels.Clear();
        _tokenModel.Clear();
        _tokenHex.Clear();
        _units = def.Build().ToList();

        _orderedIds.Clear();
        _orderedIds.AddRange(_units
            .OrderBy(u => u.Field.Owner.Value).ThenBy(u => u.Id.Value)
            .Select(u => u.Id.Value));

        _aggregate = _units.Count > 8;
        _initialA = _units.Count(u => u.Field.Owner.Value == 1);
        _initialE = _units.Count(u => u.Field.Owner.Value == 2);

        BuildTableHeader();
        foreach (var u in _units)
        {
            SpawnToken(u);
        }

        foreach (var u in _units)
        {
            var foe = _units.FirstOrDefault(o => o.Field.Owner != u.Field.Owner);
            if (foe is not null)
            {
                _tokens[u.Id.Value].FaceToward(_view.HexToWorld(foe.Field.Position));
            }
        }

        _titleLabel.Text = $"[{index + 1}/{_cases.Length}] {def.Title}";
        _noteLabel.Text = def.Note;
        FrameCamera();
        _stepButton.Disabled = false;
        _caseButton.Disabled = false;
    }

    private void SpawnToken(CombatUnit u)
    {
        var color = u.Field.Owner.Value == 1 ? Blue : Red;
        var ctrl = new UnitController3D();
        AddChild(ctrl);
        _spawned.Add(ctrl);
        ctrl.InitDisplay(_view, color, _tokenModel.GetValueOrDefault(u.Id.Value, 0), u.Field.Position);
        ctrl.TintFormation(color); // 진형을 붉은/푸른 계열로 확실히 구분

        ctrl.AddChild(MakeLabel(Tag(u), 84, 0.56f));
        var troops = MakeLabel($"{u.Pool.Active}/{u.MaxTroops}", 66, 0.42f);
        troops.HorizontalAlignment = HorizontalAlignment.Center;
        ctrl.AddChild(troops);

        var status = MakeLabel("", 60, 0.28f);
        status.HorizontalAlignment = HorizontalAlignment.Center;
        status.Modulate = new Color(1f, 0.72f, 0.35f);
        ctrl.AddChild(status);

        _tokens[u.Id.Value] = ctrl;
        _troopLabels[u.Id.Value] = troops;
        _statusLabels[u.Id.Value] = status;
        _tokenHex[u.Id.Value] = u.Field.Position;
    }

    private static Label3D MakeLabel(string text, int size, float y) => new()
    {
        Text = text,
        Font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf"),
        FontSize = size,
        PixelSize = 0.0021f,
        OutlineSize = 24,
        OutlineModulate = new Color(0f, 0f, 0f, 0.85f),
        Modulate = new Color(0.97f, 0.96f, 0.92f),
        Position = new Vector3(0f, y, 0f),
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        NoDepthTest = true,
    };

    // ── HUD (결과 표: 헤더 = 진행·유닛들, 행 = 준데미지/잔여/사용스킬) ──

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var panel = new PanelContainer { Position = new Vector2(16, 16), CustomMinimumSize = new Vector2(620, 0) };
        layer.AddChild(panel);
        var box = new VBoxContainer();
        panel.AddChild(box);

        _titleLabel = new Label { Text = "" };
        _titleLabel.AddThemeFontSizeOverride("font_size", 22);
        box.AddChild(_titleLabel);

        _noteLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _noteLabel.CustomMinimumSize = new Vector2(600, 0);
        box.AddChild(_noteLabel);

        var buttons = new HBoxContainer();
        box.AddChild(buttons);
        _stepButton = new Button { Text = "진행 ▶" };
        _stepButton.Pressed += OnStep;
        buttons.AddChild(_stepButton);
        _caseButton = new Button { Text = "케이스 ▶▶" };
        _caseButton.Pressed += () => { if (!_animating) { LoadCase((_caseIndex + 1) % _cases.Length); } };
        buttons.AddChild(_caseButton);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(600, 320) };
        box.AddChild(scroll);
        _table = new GridContainer();
        scroll.AddChild(_table);
    }

    // 케이스의 부대 배치를 담도록 카메라를 맞춘다(작은 케이스는 좁게, 대군은 넓게).
    private void FrameCamera()
    {
        var minQ = _units.Min(u => u.Field.Position.Q);
        var maxQ = _units.Max(u => u.Field.Position.Q);
        var minR = _units.Min(u => u.Field.Position.R);
        var maxR = _units.Max(u => u.Field.Position.R);
        var center = (_view.HexToWorld(new HexCoord(minQ, minR)) + _view.HexToWorld(new HexCoord(maxQ, maxR))) * 0.5f;
        var span = Mathf.Max(maxQ - minQ, maxR - minR);
        _camera.Setup(center, span * 0.7f + 6f);
    }

    private void BuildTableHeader()
    {
        foreach (var child in _table.GetChildren())
        {
            child.QueueFree();
        }

        if (_aggregate)
        {
            _table.Columns = 3;
            _table.AddChild(Cell("진행", header: true, width: 52));
            _table.AddChild(Cell("아군 A", header: true, width: 220));
            _table.AddChild(Cell("적군 E", header: true, width: 220));
            return;
        }

        _table.Columns = 1 + _orderedIds.Count;
        _table.AddChild(Cell("진행", header: true, width: 52));
        foreach (var id in _orderedIds)
        {
            var u = _units.First(x => x.Id.Value == id);
            _table.AddChild(Cell(Tag(u), header: true, width: 150));
        }
    }

    // 한 진행 결과 행: 유닛마다 [준 데미지 / 잔여 / 사용 스킬(-스킬데미지)].
    private void AddResultRow(AdvanceTurn turn)
    {
        _table.AddChild(Cell($"{_round}", header: false, width: 52));

        if (_aggregate)
        {
            _table.AddChild(FactionCell(1, _initialA));
            _table.AddChild(FactionCell(2, _initialE));
            return;
        }

        foreach (var id in _orderedIds)
        {
            var u = _units.FirstOrDefault(x => x.Id.Value == id);
            if (u is null)
            {
                _table.AddChild(Cell("—", header: false, width: 150)); // 전멸해 사라진 부대
                continue;
            }

            var uid = new UnitId(id);
            var combat = turn.Combat is not null;
            var dealt = turn.Combat?.DamageDealt.GetValueOrDefault(uid) ?? 0;

            var lines = new List<string>
            {
                combat ? $"준 −{dealt}" : "없음",
                $"잔여 {u.Pool.Active}/{u.MaxTroops}",
            };

            // 오케스트레이터가 보고한 발동 스킬(게이지가 한 진행에 차서 발동해도 확실히 잡힌다).
            if (turn.FiredActives.TryGetValue(uid, out var active))
            {
                lines.Add(active.Type == ActiveType.Strike ? $"{active.Name} −{dealt}" : active.Name);
            }
            if (turn.FiredStratagems.TryGetValue(uid, out var strat))
            {
                lines.Add($"계략 {strat.Name}");
            }
            if (turn.StratagemDamage.TryGetValue(uid, out var kd))
            {
                lines.Add($"계략피해 −{kd}");
            }
            if (turn.StatusDamage.TryGetValue(uid, out var sd))
            {
                lines.Add($"지속 −{sd}");
            }

            _table.AddChild(Cell(string.Join("\n", lines), header: false, width: 150));
        }
    }

    // 대량 전투용 진영 집계 셀: 남은 병력 합과 생존 부대 수.
    private Label FactionCell(int owner, int initial)
    {
        var units = _units.Where(u => u.Field.Owner.Value == owner).ToList();
        var troops = units.Sum(u => u.Pool.Active);
        return Cell($"병력 {troops}\n생존 {units.Count}/{initial}", header: false, width: 220);
    }

    private static Label Cell(string text, bool header, int width)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.Off,
            CustomMinimumSize = new Vector2(width, 0),
        };
        if (header)
        {
            label.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 1f));
        }

        return label;
    }
}
