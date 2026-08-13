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

    private sealed record CaseDef(string Title, string Note, System.Func<CombatUnit[]> Build,
        Dictionary<HexCoord, TerrainType>? Terrain = null,
        HexCoord? CastleAt = null, int CastleWall = 0, int CastleTroops = 0,
        int SallyAtRound = 0, HexCoord? SallyTarget = null);

    private MapView3D _view = null!;
    private CameraController3D _camera = null!;
    private AdvanceOrchestrator _orchestrator = null!;

    private IReadOnlyDictionary<string, TroopTemplate> _templates = null!;
    private IReadOnlyDictionary<string, ActiveSkill> _actives = null!;
    private IReadOnlyDictionary<string, PassiveSkill> _passives = null!;
    private IReadOnlyDictionary<string, Stratagem> _strats = null!;
    private IReadOnlyDictionary<string, General> _generals = null!;
    private readonly Dictionary<int, string> _unitGeneral = new();

    private CaseDef[] _cases = System.Array.Empty<CaseDef>();
    private int _caseIndex;
    private int _round;
    private bool _aggregate; // 부대가 많으면(대량 전투) 표를 유닛별 대신 진영 집계로
    private int _initialA;
    private int _initialE;
    private bool _showTerrain; // 지형 케이스면 표에 지형·공방을 함께 보인다
    private HexMap _terrainMap = null!;
    private readonly Dictionary<int, (TroopTemplate Template, int AtkBucket, int DfBucket)> _recipe = new();

    // 공성 케이스: 성(성벽 HP·수비 병력)과 그 시각·상태.
    private readonly BattleResolver _siegeResolver = new(60);
    private CastleState? _castle;
    private int _castleMaxWall;
    private int _castleMaxTroops;
    private HexCoord _castlePos;
    private int _castleOwner;
    private Label3D _castleLabel = null!;
    private (SiegeOutcome Outcome, List<int> BesiegerIds)? _pendingSiege;
    private readonly Dictionary<int, int> _lastSiegeCounter = new();
    private readonly List<CombatUnit> _garrison = new();
    private List<CombatUnit> _units = new();
    private readonly List<int> _orderedIds = new();
    private readonly Dictionary<int, UnitController3D> _tokens = new();
    private readonly Dictionary<int, Label3D> _troopLabels = new();
    private readonly Dictionary<int, Label3D> _statusLabels = new();
    private readonly Dictionary<int, int> _tokenModel = new();
    private readonly Dictionary<int, HexCoord> _tokenHex = new();
    private readonly List<Node3D> _spawned = new();

    // 재생 규칙: 비트(이동 1칸·공격 모션)당 1초. 이동 트윈은 비트보다 짧게 둬야(0.9<1.0) 다음
    // 공격 비트가 뜰 때 _moving이 이미 풀려, 이동한 진행의 공격 모션이 스킵되지 않는다.
    private const float BeatSeconds = 1.0f;
    private const float MoveSeconds = 0.9f;
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
        _generals = new GeneralLoader().LoadFromDirectory(dataDirectory).ToDictionary(g => g.Name);

        // 오케스트레이터는 케이스별 지형으로 LoadCase에서 만든다.
        _cases = BuildCases();
        BuildHud();

        _beatTimer = new Godot.Timer { WaitTime = BeatSeconds, OneShot = false };
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
                    var castleInfo = _castle is { } cs ? $" | 성벽{cs.WallCurrent} 수비{cs.Troops}" : "";
                    GD.Print($"[combattestauto] case {_caseIndex} after {rounds}{castleInfo}: " +
                        string.Join(" ", _units.Select(u =>
                        {
                            if (!_showTerrain)
                            {
                                return $"{Tag(u)}={u.Pool.Active}";
                            }

                            var (dAtk, dDf) = DisplayStats(u);
                            return $"{Tag(u)}[{TerrainKo(_terrainMap.TerrainAt(u.Field.Position))}]공{dAtk}방{dDf}@{u.Field.Position.Q},{u.Field.Position.R}={u.Pool.Active}";
                        })));
                    if (_caseIndex + 1 < _cases.Length) { LoadCase(_caseIndex + 1); rounds = 0; }
                    else { GD.Print("[combattestauto] all cases done"); timer.Stop(); GetTree().Quit(); }
                    return;
                }

                rounds++;
                BeginTurn();
                FinalizeTurn();
                if (_castle is { } rc)
                {
                    GD.Print($"[round] case {_caseIndex} r{rounds} 성벽{rc.WallCurrent} 수비{rc.Troops} | " +
                        string.Join(" ", _units.Select(u => $"{Tag(u)}@{u.Field.Position.Q},{u.Field.Position.R}={u.Pool.Active}")));
                }
            };
        }
    }

    // ── 케이스 ──

    // 장수 기반 부대: 적성은 선봉의 병종별 통솔, 스킬은 선봉·부관 모두(UnitAssembler).
    private CombatUnit UnitG(int id, int owner, HexCoord pos, string templateCode, HexCoord? target, UnitMode mode,
        string vanguard, string? adjutant = null, int troops = 10000)
    {
        var template = _templates[templateCode];
        var van = _generals[vanguard];
        var adj = adjutant is null ? null : _generals[adjutant];

        var held = van.Passives.Concat(adj?.Passives ?? System.Array.Empty<GeneralSkill>())
            .Select(s => (_passives[s.Code], s.Tier));
        var (atk, df) = PassiveBucketEvaluator.Evaluate(held, MeleeCtx);
        _recipe[id] = (template, atk, df);
        _tokenModel[id] = ModelIndex.GetValueOrDefault(templateCode, 0);
        _unitGeneral[id] = adjutant is null ? vanguard : $"{vanguard}·{adjutant}";

        return UnitAssembler.Assemble(new UnitId(id), new FactionId(owner), pos, mode, target, id,
            van, adj, template, troops, _actives, _passives, MeleeCtx);
    }

    private CombatUnit Unit(int id, int owner, HexCoord pos, string templateCode, HexCoord? target, UnitMode mode,
        string passiveCode, string activeCode, int might = 60, int intellect = 60, int troops = 10000,
        string? stratagemCode = null, int stratagemTarget = 0)
    {
        var template = _templates[templateCode];
        var (atk, df) = PassiveBucketEvaluator.Evaluate(new[] { (_passives[passiveCode], 3) }, MeleeCtx);
        _recipe[id] = (template, atk, df);
        // 스탯엔 지형 보정을 넣지 않는다(중립 River=0). 지형 공방 보정은 전투 시점에 오케스트레이터가
        // 이동 후 위치·병종 분류로 얹는다 — 이동 중인 부대도 실제 전투 칸의 보정을 받는다.
        var stats = CombatStatsBuilder.BuildField(template, AptitudeGrade.A, 0, TerrainType.River,
            troops, atkBonusPercent: atk, dfBonusPercent: df);
        // 속도·탐지·사거리(유닛)를 병종 데이터에서 실제로 반영한다.
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos,
            template.MovementPerDay, template.Detection, template.RangeUnit,
            MovementDomain.Land, mode, target, id, template.RangeCastle);
        _tokenModel[id] = ModelIndex.GetValueOrDefault(templateCode, 0);
        var state = UnitCombatState.Create(intellect, vanguardActive: _actives[activeCode]);
        if (stratagemCode is not null)
        {
            state = state.ReserveStratagem(_strats[stratagemCode], new UnitId(stratagemTarget));
        }

        return new CombatUnit(field, stats, new TroopPool(troops, 0), state, might, intellect, MaxTroops: troops, Class: template.Class);
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
        new CaseDef("대량 전투 — 혼성군 충돌",
            "아군 A·적군 E가 혼성군(각 11기: 기병2·도검4·상병2·궁병2·투석1)으로 마주 진격. 병종별 실제 속도로 기병(3)이 먼저 달려들고 투석기(1)가 뒤늦게 따라오며, 궁병·투석은 사거리 2로 한 칸 뒤에서 친다. 전멸분은 토큰이 사라진다. 표는 진영 집계.",
            BigBattle),
        new CaseDef("지형 + 5병종 — 공방·이동 보정",
            "공격군 A·방어군 E 각 5병종(궁병·도검·기병·상병·투석). 방어군 E는 지형 위 포진 — 궁병=숲(+2공/+2방)·도검=소형산(+2공)·기병=평야(+2공)·상병=늪. 공격군 A는 소하천 띠(6열)를 건너며 감속(진입 시 그 날 예산 소진). 표는 부대별로 [지형] 공/방·잔여를 보인다.",
            () =>
            {
                CombatUnit Def(int id, HexCoord pos, string code) =>
                    Unit(id, 2, pos, code, null, UnitMode.Advance, "steadfast_guard", "iron_wall", might: 70, intellect: 70, troops: 20000);
                CombatUnit Atk(int id, HexCoord pos, HexCoord tgt, string code) =>
                    Unit(id, 1, pos, code, tgt, UnitMode.Attack, "fierce_assault", "peerless", might: 78);
                return new[]
                {
                    Atk(1, new HexCoord(2, 2), new HexCoord(11, 2), "archer"),
                    Atk(2, new HexCoord(2, 3), new HexCoord(11, 3), "swordsman"),
                    Atk(3, new HexCoord(2, 4), new HexCoord(11, 4), "cavalry"),
                    Atk(4, new HexCoord(2, 5), new HexCoord(11, 5), "war_elephant"),
                    Atk(5, new HexCoord(2, 6), new HexCoord(11, 6), "catapult"),
                    Def(11, new HexCoord(11, 2), "archer"),
                    Def(12, new HexCoord(11, 3), "swordsman"),
                    Def(13, new HexCoord(11, 4), "cavalry"),
                    Def(14, new HexCoord(11, 5), "war_elephant"),
                    Def(15, new HexCoord(11, 6), "catapult"),
                };
            },
            Terrain: new Dictionary<HexCoord, TerrainType>
            {
                [new HexCoord(11, 2)] = TerrainType.Forest,
                [new HexCoord(11, 3)] = TerrainType.Mountain,
                [new HexCoord(11, 5)] = TerrainType.Swamp,
                [new HexCoord(6, 2)] = TerrainType.River,
                [new HexCoord(6, 3)] = TerrainType.River,
                [new HexCoord(6, 4)] = TerrainType.River,
                [new HexCoord(6, 5)] = TerrainType.River,
                [new HexCoord(6, 6)] = TerrainType.River,
            }),
        new CaseDef("공성 — 성벽 격파 → 함락",
            "공격군이 성으로 진격해 성벽(6000)을 두들긴다. 투석기·공성탑은 사거리 2에서 반격 없이 깎고(성 반격 사거리=1), 도검병은 인접해 반격을 받는다. 성벽이 무너지면(붕괴) 수비 병력(10000) 직격으로 넘어간다. 성벽/수비는 성 위 라벨.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(2, 3), "catapult", new HexCoord(11, 4), UnitMode.Attack, "steadfast_guard", "regroup", intellect: 70),
                Unit(2, 1, new HexCoord(2, 5), "catapult", new HexCoord(11, 5), UnitMode.Attack, "steadfast_guard", "regroup", intellect: 70),
                Unit(3, 1, new HexCoord(2, 4), "siege_tower", new HexCoord(11, 6), UnitMode.Attack, "steadfast_guard", "regroup"),
                Unit(4, 1, new HexCoord(1, 4), "swordsman", new HexCoord(12, 4), UnitMode.Attack, "fierce_assault", "peerless", might: 78),
            },
            CastleAt: new HexCoord(13, 4), CastleWall: 6000, CastleTroops: 10000),
        new CaseDef("공성 — 일반 병력 공략",
            "공성 병기 없이 일반 병종(도검2·기병·상병)이 얇은 성벽(3000)을 친다. 전부 사거리 1이라 인접해서 성벽을 깎고 성의 반격을 받는다(공성 병기와 달리 피해를 감수). 성벽이 무너지면 수비 병력(10000) 직격 → 함락.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(2, 3), "swordsman", new HexCoord(12, 4), UnitMode.Attack, "steadfast_guard", "iron_wall", might: 78),
                Unit(2, 1, new HexCoord(2, 5), "swordsman", new HexCoord(13, 3), UnitMode.Attack, "steadfast_guard", "iron_wall", might: 78),
                Unit(3, 1, new HexCoord(2, 4), "cavalry", new HexCoord(13, 5), UnitMode.Attack, "steadfast_guard", "iron_wall", might: 80),
                Unit(4, 1, new HexCoord(1, 4), "war_elephant", new HexCoord(12, 5), UnitMode.Attack, "steadfast_guard", "regroup", might: 80, intellect: 70),
            },
            CastleAt: new HexCoord(13, 4), CastleWall: 3000, CastleTroops: 10000),
        new CaseDef("공성 — 수비대 입성",
            "성 밖 궁병(E9)이 자기 성으로 복귀해 입성(이동 단계 처리) — 병력 6000이 수비에 합류해 수비 4000→10000, "
            + "같은 진행의 성 반격부터 두꺼워진 수비로 계산된다. 입성 시 성 복귀 초기화(게이지·모략력·지속 상태 해제).",
            () => new[]
            {
                Unit(1, 1, new HexCoord(2, 3), "swordsman", new HexCoord(12, 4), UnitMode.Attack, "steadfast_guard", "iron_wall", might: 78),
                Unit(2, 1, new HexCoord(2, 5), "swordsman", new HexCoord(13, 3), UnitMode.Attack, "steadfast_guard", "iron_wall", might: 78),
                Unit(3, 1, new HexCoord(2, 4), "cavalry", new HexCoord(13, 5), UnitMode.Attack, "steadfast_guard", "iron_wall", might: 80),
                Unit(4, 1, new HexCoord(1, 4), "war_elephant", new HexCoord(12, 5), UnitMode.Attack, "steadfast_guard", "regroup", might: 80, intellect: 70),
                Unit(9, 2, new HexCoord(11, 6), "archer", new HexCoord(13, 4), UnitMode.March, "steadfast_guard", "regroup", troops: 6000),
            },
            CastleAt: new HexCoord(13, 4), CastleWall: 3000, CastleTroops: 4000),
        new CaseDef("공성 — 수비대 출격",
            "궁병(E9)이 1진행에 입성해 수비에 합류(4000→10000)했다가, 3진행에 출격 — 입성 병력 6000을 "
            + "수비에서 도로 빼(수비 4000) 성 타일에서 걸어 나와 공격군과 야전을 벌인다. 출격도 이동: "
            + "성에서 나오는 첫 스텝부터 그 진행의 이동력을 쓴다.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(2, 3), "swordsman", new HexCoord(12, 4), UnitMode.Attack, "steadfast_guard", "iron_wall", might: 78),
                Unit(2, 1, new HexCoord(2, 5), "swordsman", new HexCoord(13, 3), UnitMode.Attack, "steadfast_guard", "iron_wall", might: 78),
                Unit(3, 1, new HexCoord(2, 4), "cavalry", new HexCoord(13, 5), UnitMode.Attack, "steadfast_guard", "iron_wall", might: 80),
                Unit(4, 1, new HexCoord(1, 4), "war_elephant", new HexCoord(12, 5), UnitMode.Attack, "steadfast_guard", "regroup", might: 80, intellect: 70),
                Unit(9, 2, new HexCoord(11, 6), "archer", new HexCoord(13, 4), UnitMode.March, "steadfast_guard", "regroup", troops: 6000),
            },
            CastleAt: new HexCoord(13, 4), CastleWall: 3000, CastleTroops: 4000,
            SallyAtRound: 3, SallyTarget: new HexCoord(10, 4)),
        new CaseDef("함락 — 복귀 중단",
            "얇은 성(성벽 500·수비 1500)이 도검(A1)에게 빠르게 함락·점거된다. 멀리서 행군으로 복귀하던 "
            + "궁병(E8)은 성이 함락되면 다음 진행에 멈춤(전진 대형·목표 해제)으로 전환되어 그 자리에 선다 — "
            + "적 소유가 된 성 타일로는 들어가지 않는다.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(10, 3), "swordsman", new HexCoord(12, 4), UnitMode.Attack, "steadfast_guard", "iron_wall", might: 78),
                Unit(8, 2, new HexCoord(2, 6), "archer", new HexCoord(13, 4), UnitMode.March, "steadfast_guard", "regroup", troops: 5000),
            },
            CastleAt: new HexCoord(13, 4), CastleWall: 500, CastleTroops: 1500),
        new CaseDef("장수 편성 대전",
            "부대 = 선봉(+부관) 장수 + 병종. 적성은 선봉의 병종별 통솔(여포 기병 SS=130% vs 유비 기병 A=95%), "
            + "액티브·패시브는 두 장수 모두, 무력·지력은 선봉 기준. 관우+제갈량은 부관 철벽까지 두 액티브를 쓴다.",
            () => new[]
            {
                UnitG(1, 1, new HexCoord(3, 2), "swordsman", new HexCoord(11, 2), UnitMode.Attack, "관우", "제갈량"),
                UnitG(2, 1, new HexCoord(3, 4), "cavalry", new HexCoord(11, 4), UnitMode.Attack, "조운"),
                UnitG(3, 1, new HexCoord(3, 6), "archer", new HexCoord(11, 6), UnitMode.Attack, "황충"),
                UnitG(11, 2, new HexCoord(12, 2), "swordsman", new HexCoord(4, 2), UnitMode.Attack, "조조", "사마의"),
                UnitG(12, 2, new HexCoord(12, 4), "cavalry", new HexCoord(4, 4), UnitMode.Attack, "여포"),
                UnitG(13, 2, new HexCoord(12, 6), "swordsman", new HexCoord(4, 6), UnitMode.Attack, "장비"),
            }),
    };

    // 양 진영 혼성군(각 11기). 기병(속도3)은 양 날개 최전열, 도검(2)·상병(2)은 주력 전열,
    // 궁병(2·사거리2)·투석기(1·사거리2)는 후열. 각자 반대편으로 진격(공격모드) — 속도·사거리
    // 차이가 눈에 보이도록 배치.
    private CombatUnit[] BigBattle()
    {
        var list = new List<CombatUnit>();
        var id = 1;

        void Side(int owner, int backQ, int lineQ, int flankQ, int goalQ)
        {
            list.Add(Unit(id++, owner, new HexCoord(flankQ, 1), "cavalry", new HexCoord(goalQ, 1), UnitMode.Attack, "fierce_assault", "peerless", might: 82));
            list.Add(Unit(id++, owner, new HexCoord(flankQ, 8), "cavalry", new HexCoord(goalQ, 8), UnitMode.Attack, "fierce_assault", "peerless", might: 82));
            foreach (var r in new[] { 2, 3, 6, 7 })
            {
                list.Add(Unit(id++, owner, new HexCoord(lineQ, r), "swordsman", new HexCoord(goalQ, r), UnitMode.Attack, "fierce_assault", "peerless", might: 76));
            }

            foreach (var r in new[] { 4, 5 })
            {
                list.Add(Unit(id++, owner, new HexCoord(lineQ, r), "war_elephant", new HexCoord(goalQ, r), UnitMode.Attack, "steadfast_guard", "regroup", might: 80, intellect: 70));
            }

            foreach (var r in new[] { 3, 6 })
            {
                list.Add(Unit(id++, owner, new HexCoord(backQ, r), "archer", new HexCoord(goalQ, r), UnitMode.Attack, "fierce_assault", "regroup", might: 72));
            }

            list.Add(Unit(id++, owner, new HexCoord(backQ, 5), "catapult", new HexCoord(goalQ, 5), UnitMode.Attack, "steadfast_guard", "regroup", might: 74));
        }

        Side(1, backQ: 0, lineQ: 1, flankQ: 2, goalQ: 15);
        Side(2, backQ: 16, lineQ: 15, flankQ: 14, goalQ: 1);
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

        // 아군 성으로 행군 복귀 중 성이 함락됐으면(소유가 바뀜) 다음 진행에 멈춘다
        // (전진 대형 + 목표 해제). 공격모드 부대는 그대로 둔다 — 그쪽은 점거된 성을 친다.
        if (_castle is not null)
        {
            for (var i = 0; i < _units.Count; i++)
            {
                var u = _units[i];
                if (u.Field.Mode == UnitMode.March && u.Field.Target == _castlePos
                    && u.Field.Owner.Value != _castleOwner)
                {
                    _units[i] = u with { Field = u.Field with { Mode = UnitMode.Advance, Target = null } };
                    _noteLabel.Text = $"{Tag(u)} 복귀 중단 — 성 함락, 멈춤";
                }
            }
        }

        // 수비대 출격 — 이동과 같은 단계: 진행 계산 전에 성 타일에 올라서고, 이번 진행의
        // 이동력으로 걸어 나온다. 병력은 입성 때 그대로(성 수비가 그보다 줄었으면 남은 만큼만).
        if (_cases[_caseIndex].SallyAtRound == _round && _garrison.Count > 0 && _castle is { } gc)
        {
            foreach (var g in _garrison.ToList())
            {
                var troops = System.Math.Min(g.Pool.Active, gc.Troops);
                if (troops <= 0)
                {
                    // 출격 불가 ② — 빼줄 수비 병력이 없다(공성으로 수비가 바닥). 성 안에 남는다.
                    _noteLabel.Text = $"{Tag(g)} 출격 불가 — 수비 병력 없음";
                    continue;
                }

                gc = gc with { Troops = gc.Troops - troops };
                var unit = g with
                {
                    Pool = new TroopPool(troops, g.Pool.Wounded),
                    Field = g.Field with
                    {
                        Position = _castlePos,
                        Mode = UnitMode.Attack,
                        Target = _cases[_caseIndex].SallyTarget,
                    },
                };
                _units.Add(unit);
                _garrison.Remove(g);
                SpawnToken(unit);
                _noteLabel.Text = $"{Tag(unit)} 출격 — 수비 −{troops}";
            }

            _castle = gc;
            RefreshCastleLabel();
        }

        var sites = _castle is { } c && (c.WallCurrent > 0 || c.Troops > 0)
            ? new[] { new SiegeSite(_castlePos, new FactionId(_castleOwner)) }
            : null;
        _pending = _orchestrator.Run(_units, castles: sites);

        // 수비 합류는 이동 단계(입성)의 일부 — 같은 진행의 공성 반격 계산(ComputeSiege)에 반영되도록
        // 여기서 성에 합산한다. 토큰 정리·표기는 FinalizeTurn이 한다. 부대는 수비대로 보관한다(출격용).
        foreach (var u in _pending.EnteredCastle)
        {
            if (_castle is { } cs)
            {
                _castle = cs with { Troops = cs.Troops + u.Pool.Active };
            }

            _garrison.Add(u);
        }

        _beats = new Queue<System.Action>();
        // 실제로 위치가 바뀌는 틱만 이동 비트로 넣는다(정지/교전 스냅샷은 건너뛴다).
        // 입성(EnteredCastle) 틱은 그 시점 비트로 토큰을 거둔다 — 진행 끝까지 남겨두면
        // 코어에선 비어 있는 칸에 다른 부대가 들어와 화면만 겹쳐 보인다.
        var running = new Dictionary<int, HexCoord>(_tokenHex);
        var enteredNotes = _pending.EnteredCastle.ToDictionary(
            u => u.Id.Value, u => $"{Tag(u)} 입성 — 수비 +{u.Pool.Active}");
        foreach (var tick in _pending.Movement.Ticks)
        {
            var enteredNow = tick.Events
                .Where(e => e.Kind == TickEventKind.EnteredCastle)
                .Select(e => e.Unit.Value)
                .ToList();
            var moves = tick.Units.Any(fu => running.GetValueOrDefault(fu.Id.Value, fu.Position) != fu.Position);
            if (!moves && enteredNow.Count == 0)
            {
                continue;
            }

            var snapshot = tick;
            if (moves)
            {
                _beats.Enqueue(() => MoveTokens(snapshot));
                foreach (var fu in tick.Units)
                {
                    running[fu.Id.Value] = fu.Position;
                }
            }

            if (enteredNow.Count > 0)
            {
                _beats.Enqueue(() =>
                {
                    foreach (var idv in enteredNow)
                    {
                        _noteLabel.Text = enteredNotes.GetValueOrDefault(idv, "입성");
                        DespawnToken(idv);
                    }

                    RefreshCastleLabel();
                });
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

        if (_castle is not null)
        {
            ComputeSiege();
            if (_pendingSiege is not null)
            {
                _beats.Enqueue(PlaySiege);
            }
        }
    }

    // 표시용: 부대의 현재 칸 지형 공방 보정을 반영한 공/방(전투에서 오케스트레이터가 쓰는 값과 같다).
    private (int Atk, int Df) DisplayStats(CombatUnit u)
    {
        var (t, _, _) = _recipe[u.Id.Value];
        var (tAtk, tDf) = TerrainCombatBonus.For(t.Class, _terrainMap.TerrainAt(u.Field.Position));
        return (u.Stats.AtkStat + tAtk, u.Stats.DfStat + tDf);
    }

    // 이동 시뮬이 잡지 못한 위치 변화(교란 후퇴)를 토큰에 반영한다.
    private void SettleTokens()
    {
        foreach (var u in _pending!.Units)
        {
            if (_tokens.TryGetValue(u.Id.Value, out var ctrl)
                && _tokenHex.GetValueOrDefault(u.Id.Value, u.Field.Position) != u.Field.Position)
            {
                ctrl.DisplayStepTo(u.Field.Position, MoveSeconds);
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

            ctrl.DisplayStepTo(fu.Position, MoveSeconds);
            _tokenHex[fu.Id.Value] = fu.Position;
            // 가장 가까운 적을 향한다(대군에서 엉뚱한 방향으로 서지 않도록). 이동 중 회전은
            // AnimateMarch가 진행 방향으로 덮어쓴다.
            var foe = tick.Units
                .Where(o => o.Owner.Value != fu.Owner.Value)
                .OrderBy(o => o.Position.Distance(fu.Position))
                .ThenBy(o => o.Id.Value)
                .FirstOrDefault();
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

            // 실제 교전 상대(가장 가까운 살아있는 적)를 향해 돌아선 뒤 공격한다 — 기병 돌격·궁병 사격
            // 방향이 엉뚱한 적을 향하지 않도록. 돌격 중엔 회전이 잠기므로 이 방향이 곧 돌격 방향이다.
            var foe = units
                .Where(o => o.Field.Owner.Value != u.Field.Owner.Value && o.Pool.Active > 0)
                .OrderBy(o => o.Field.Position.Distance(u.Field.Position))
                .ThenBy(o => o.Id.Value)
                .FirstOrDefault();
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

        // 아군 성 입성 정리: 토큰을 거두고 라벨·안내를 갱신한다(수비 합산은 BeginTurn에서 완료).
        foreach (var u in turn.EnteredCastle)
        {
            _noteLabel.Text = $"{Tag(u)} 입성 — 수비 +{u.Pool.Active}";
            DespawnToken(u.Id.Value);
            RefreshCastleLabel();
        }

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

        // 공성 반영을 표보다 먼저 — 반격 피해가 그 진행의 행(잔여)에 바로 실린다.
        _lastSiegeCounter.Clear();
        ApplySiege();
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
    {
        // 살아 있는 성(수비 > 0)은 그 소유 세력의 참전으로 센다 — 수성 중엔 공격군이 전멸해야
        // 끝나고, 함락·점거 후엔 성과 야전 부대가 같은 세력이면 종료된다.
        var owners = _units.Where(u => u.Pool.Active > 0).Select(u => u.Field.Owner.Value).ToHashSet();
        if (_castle is { } c && (c.WallCurrent > 0 || c.Troops > 0))
        {
            owners.Add(_castleOwner);
        }

        return owners.Count < 2;
    }

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
        _recipe.Clear();
        _garrison.Clear();
        _unitGeneral.Clear();

        // 케이스별 지형으로 오케스트레이터를 만든다(이동 패널티·전투 보정 모두 이 맵을 본다).
        _showTerrain = def.Terrain is not null;
        _terrainMap = new HexMap(0, MaxQ, 0, MaxR, def.Terrain);

        // 공성 케이스: 성을 세운다(발자국은 통행 불가라 공격군이 밖에서 멈춘다).
        _pendingSiege = null;
        var cities = new List<City>();
        if (def.CastleAt is { } anchor)
        {
            _castlePos = anchor;
            _castleOwner = 2;
            _castleMaxWall = def.CastleWall;
            _castleMaxTroops = def.CastleTroops;
            _castle = new CastleState(def.CastleWall, def.CastleTroops);
            cities.Add(new City(new CityId(99), "성", anchor, new FactionId(2), 0, CastleSize.Small));
        }
        else
        {
            _castle = null;
        }

        _orchestrator = new AdvanceOrchestrator(
            new MovementSimulator(new PassabilityMap(_terrainMap, [], cities)),
            new CombatPhaseResolver(new BattleResolver(60), woundedPercent: 70),
            woundedPercent: 70,
            terrainAt: _terrainMap.TerrainAt);

        _units = def.Build().ToList(); // Unit() 안에서 _terrainMap·_recipe를 쓴다 — 위에서 먼저 세팅
        SpawnTerrainMarkers();
        SpawnCastle();

        _orderedIds.Clear();
        _orderedIds.AddRange(_units
            .OrderBy(u => u.Field.Owner.Value).ThenBy(u => u.Id.Value)
            .Select(u => u.Id.Value));

        _aggregate = _units.Count > 8 && !_showTerrain; // 지형 케이스는 부대별로 본다
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

    // 지형 케이스: 평야가 아닌 타일에 색 마커를 깔아 지형 위치를 눈으로 보인다.
    private void SpawnTerrainMarkers()
    {
        if (!_showTerrain)
        {
            return;
        }

        foreach (var tile in _terrainMap.Tiles())
        {
            var terrain = _terrainMap.TerrainAt(tile);
            if (terrain == TerrainType.Plains)
            {
                continue;
            }

            var marker = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.52f, BottomRadius = 0.52f, Height = 0.05f, RadialSegments = 6 },
                Position = _view.HexToWorld(tile) + new Vector3(0f, 0.04f, 0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = TerrainColor(terrain),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                },
            };
            AddChild(marker);
            _spawned.Add(marker);
        }
    }

    private static Color TerrainColor(TerrainType t) => t switch
    {
        TerrainType.Forest => new Color(0.20f, 0.60f, 0.26f, 0.55f),
        TerrainType.Mountain => new Color(0.55f, 0.48f, 0.42f, 0.62f),
        TerrainType.Swamp => new Color(0.34f, 0.30f, 0.14f, 0.62f),
        TerrainType.River => new Color(0.25f, 0.52f, 0.86f, 0.55f),
        TerrainType.Desert => new Color(0.85f, 0.78f, 0.45f, 0.55f),
        _ => new Color(0.6f, 0.6f, 0.6f, 0.5f),
    };

    private static string TerrainKo(TerrainType t) => t switch
    {
        TerrainType.Plains => "평야",
        TerrainType.Forest => "숲",
        TerrainType.Mountain => "소형산",
        TerrainType.Swamp => "늪",
        TerrainType.River => "소하천",
        TerrainType.Desert => "사막",
        _ => t.ToString(),
    };

    // 공성 케이스: 성 모델 + 성벽/수비 라벨을 세운다.
    private void SpawnCastle()
    {
        if (_castle is null)
        {
            return;
        }

        var node = GD.Load<PackedScene>("res://assets/models/castle-small.glb").Instantiate<Node3D>();
        node.Position = _view.HexToWorld(_castlePos) + new Vector3(0f, _view.TileTopY, 0f);
        AddChild(node);
        _spawned.Add(node);
        MapView3D.TuneImportedMeshes(node);

        _castleLabel = MakeLabel("", 70, 1.15f);
        _castleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _castleLabel.Modulate = new Color(1f, 0.85f, 0.4f);
        node.AddChild(_castleLabel);
        RefreshCastleLabel();
    }

    private void RefreshCastleLabel()
    {
        if (_castle is not { } c)
        {
            return;
        }

        _castleLabel.Text = c.WallCurrent > 0
            ? $"성벽 {c.WallCurrent}/{_castleMaxWall}\n수비 {c.Troops}"
            : c.Troops > 0 ? $"성벽 붕괴\n수비 {c.Troops}" : "함락";
        _castleLabel.Modulate = c.WallCurrent > 0 ? new Color(1f, 0.85f, 0.4f)
            : c.Troops > 0 ? new Color(1f, 0.55f, 0.3f) : new Color(0.9f, 0.4f, 0.35f);
    }

    private int FootprintDist(HexCoord p) => p.Distance(_castlePos); // 소형성 = 1타일

    // 진행 결과 위치에서 성 사거리 안 공격 부대를 모아 공성 1교환을 계산해 둔다(적용은 FinalizeTurn).
    private void ComputeSiege()
    {
        _pendingSiege = null;
        if (_castle is not { } castle || (castle.WallCurrent <= 0 && castle.Troops <= 0))
        {
            return;
        }

        var besiegers = _pending!.Units
            .Where(u => u.Field.Owner.Value != _castleOwner && u.Pool.Active > 0
                && FootprintDist(u.Field.Position) <= _recipe[u.Id.Value].Template.RangeCastle)
            .OrderBy(u => u.Id.Value)
            .ToList();
        if (besiegers.Count == 0)
        {
            return;
        }

        var attackers = besiegers.Select(u =>
        {
            var (t, atk, df) = _recipe[u.Id.Value];
            return CombatStatsBuilder.BuildSiegeAttacker(t, AptitudeGrade.A, 0, _terrainMap.TerrainAt(u.Field.Position),
                u.Pool.Active, inCounterRange: FootprintDist(u.Field.Position) <= 1, atkBonusPercent: atk, dfBonusPercent: df);
        }).ToList();

        _pendingSiege = (_siegeResolver.ResolveSiege(attackers, castle), besiegers.Select(u => u.Id.Value).ToList());
    }

    // 공성 애니메이션: 성을 향해 돌아 공격 모션.
    private void PlaySiege()
    {
        if (_pendingSiege is not { } s)
        {
            return;
        }

        foreach (var idv in s.BesiegerIds)
        {
            if (_tokens.TryGetValue(idv, out var ctrl))
            {
                ctrl.FaceToward(_view.HexToWorld(_castlePos));
                ctrl.PlayAttackMotion();
            }
        }
    }

    // 공성 결과 반영: 성벽·수비 병력 갱신, 공격 부대 반격 피해.
    private void ApplySiege()
    {
        if (_pendingSiege is not { } s || _castle is not { } castle)
        {
            return;
        }

        var o = s.Outcome;
        _castle = castle with { WallCurrent = o.NewWall, Troops = System.Math.Max(0, castle.Troops - o.TroopDamage) };
        for (var i = 0; i < s.BesiegerIds.Count; i++)
        {
            var idx = _units.FindIndex(u => u.Id.Value == s.BesiegerIds[i]);
            if (idx < 0)
            {
                continue;
            }

            _lastSiegeCounter[s.BesiegerIds[i]] = o.CounterDamage[i];
            if (o.CounterDamage[i] > 0)
            {
                _units[idx] = _units[idx] with { Pool = _units[idx].Pool.TakeDamage(o.CounterDamage[i], 70) };
                RefreshLabel(_units[idx]);
            }
        }

        _noteLabel.Text = o.WallStanding
            ? $"공성: 성벽 −{o.WallDamage} (남은 {o.NewWall})"
            : $"공성(붕괴): 수비 −{o.TroopDamage}";
        RefreshCastleLabel();
        _pendingSiege = null;

        if (_castle is { WallCurrent: <= 0, Troops: <= 0 })
        {
            CaptureCastle();
        }
    }

    // 함락(수비 0) 처리: ① 근접(거리 1) 공격 부대는 자동 입성해 성을 점거한다 — 병력·병종·부상
    // 데이터는 수비대로 그대로 보관(성 복귀 초기화 적용). 성 소유가 점거 세력으로 바뀌고 수비는
    // 입성 병력 합, 성벽은 깨진 채(0) 남는다. ② 근접하지 않은 그 세력의 공격모드 부대는 전부
    // 멈춤(전진 대형 + 목표 해제)으로 전환한다 — 서서 방어만 하고 추격·선공하지 않는다.
    private void CaptureCastle()
    {
        var adjacent = _units
            .Where(u => u.Field.Owner.Value != _castleOwner && u.Pool.Active > 0
                && FootprintDist(u.Field.Position) <= 1)
            .OrderBy(u => u.Id.Value)
            .ToList();
        if (adjacent.Count == 0)
        {
            return; // 근접 부대가 없으면 점거 없이 빈 성(수비 0)으로 남는다
        }

        var newOwner = adjacent[0].Field.Owner.Value;
        foreach (var u in adjacent)
        {
            _units.Remove(u);
            _garrison.Add(u with { State = u.State.ReturnToCastle() });
            DespawnToken(u.Id.Value);
        }

        _castleOwner = newOwner;
        _castle = new CastleState(0, adjacent.Sum(u => u.Pool.Active));

        for (var i = 0; i < _units.Count; i++)
        {
            var u = _units[i];
            if (u.Field.Owner.Value == newOwner && u.Field.Mode == UnitMode.Attack)
            {
                _units[i] = u with { Field = u.Field with { Mode = UnitMode.Advance, Target = null } };
            }
        }

        _noteLabel.Text = $"함락! {string.Join("·", adjacent.Select(Tag))} 입성 점거 — 수비 {_castle!.Troops}";
        RefreshCastleLabel();
    }

    private void SpawnToken(CombatUnit u)
    {
        var color = u.Field.Owner.Value == 1 ? Blue : Red;
        var ctrl = new UnitController3D();
        AddChild(ctrl);
        _spawned.Add(ctrl);
        ctrl.InitDisplay(_view, color, _tokenModel.GetValueOrDefault(u.Id.Value, 0), u.Field.Position);
        ctrl.TintFormation(color); // 진형을 붉은/푸른 계열로 확실히 구분

        var tagText = _unitGeneral.TryGetValue(u.Id.Value, out var generalName)
            ? $"{Tag(u)} {generalName}"
            : Tag(u);
        ctrl.AddChild(MakeLabel(tagText, 84, 0.56f));
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
            var label = _showTerrain ? $"{Tag(u)} {_recipe[id].Template.Name}" : Tag(u);
            _table.AddChild(Cell(label, header: true, width: 150));
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
            if (turn.EnteredCastle.FirstOrDefault(e => e.Id.Value == id) is { } entered)
            {
                _table.AddChild(Cell($"입성\n수비 +{entered.Pool.Active}", header: false, width: 150));
                continue;
            }

            var u = _units.FirstOrDefault(x => x.Id.Value == id);
            if (u is null)
            {
                _table.AddChild(Cell("—", header: false, width: 150)); // 전멸·입성으로 야전에 없는 부대
                continue;
            }

            var uid = new UnitId(id);
            var combat = turn.Combat is not null;
            var dealt = turn.Combat?.DamageDealt.GetValueOrDefault(uid) ?? 0;

            var lines = new List<string>();
            if (_showTerrain)
            {
                // 지금 서 있는 칸의 지형과, 그 지형 보정이 반영된 공/방(전투 사용값과 동일).
                var (dAtk, dDf) = DisplayStats(u);
                lines.Add($"[{TerrainKo(_terrainMap.TerrainAt(u.Field.Position))}] 공{dAtk} 방{dDf}");
            }

            if (_lastSiegeCounter.TryGetValue(id, out var counter))
            {
                lines.Add(counter > 0 ? $"공성 (반격 −{counter})" : "공성");
            }
            else
            {
                lines.Add(combat ? $"준 −{dealt}" : "없음");
            }

            lines.Add($"잔여 {u.Pool.Active}/{u.MaxTroops}");

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
