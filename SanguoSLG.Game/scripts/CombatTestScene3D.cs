using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 전투 검증 하베스트. Core <see cref="AdvanceOrchestrator"/>가 한 "진행"(이동 → 계략 → 전투 페이즈 →
/// 정산)을 계산하면, 그 결과(병력 감소·부상·발동한 액티브/계략)를 3D로 보여준다. "진행"을 누를 때마다
/// 한 라운드가 진행돼 부대가 실제로 깎여나간다. 규칙·수치는 전부 Core가 소유한다(표현 전용).
/// </summary>
public partial class CombatTestScene3D : Node3D
{
    private static readonly Color Blue = new(0.24f, 0.44f, 0.86f);
    private static readonly Color Red = new(0.82f, 0.22f, 0.18f);
    private const int SwordsmanTroopIndex = 0;
    private const int MaxQ = 6;
    private const int MaxR = 2;

    private sealed record CaseDef(string Title, string Note, System.Func<CombatUnit[]> Build);

    private MapView3D _view = null!;
    private CameraController3D _camera = null!;
    private AdvanceOrchestrator _orchestrator = null!;

    private IReadOnlyDictionary<string, TroopTemplate> _templates = null!;
    private IReadOnlyDictionary<string, ActiveSkill> _actives = null!;
    private IReadOnlyDictionary<string, Stratagem> _stratagems = null!;
    private IReadOnlyDictionary<string, SpecialUnit> _specials = null!;

    // 병종 코드 → UnitController3D의 troop 모델 인덱스(토큰을 병종에 맞춘다).
    private static readonly Dictionary<string, int> ModelIndex = new()
    {
        ["swordsman"] = 0, ["cavalry"] = 1, ["archer"] = 2, ["thunder_cart"] = 3,
        ["catapult"] = 4, ["siege_tower"] = 5, ["war_elephant"] = 6, ["small_boat"] = 7,
        ["medium_ship"] = 8, ["large_ship"] = 9, ["turtleship"] = 17,
    };

    private readonly Dictionary<int, int> _tokenModel = new();

    private CaseDef[] _cases = System.Array.Empty<CaseDef>();
    private int _caseIndex;
    private List<CombatUnit> _units = new();
    private readonly Dictionary<int, UnitController3D> _tokens = new();
    private readonly Dictionary<int, Label3D> _troopLabels = new();
    private readonly List<Node3D> _spawned = new();

    private Button _stepButton = null!;
    private Button _caseButton = null!;
    private Label _titleLabel = null!;
    private Label _noteLabel = null!;
    private Label _logLabel = null!;
    private readonly List<string> _logLines = new();

    public void Build(MapView3D view, CameraController3D camera, string dataDirectory)
    {
        _view = view;
        _camera = camera;

        _templates = new TroopTypeLoader().LoadFromDirectory(dataDirectory).ToDictionary(t => t.Code);
        _actives = new ActiveSkillLoader().LoadFromDirectory(dataDirectory).ToDictionary(a => a.Code);
        _stratagems = new StratagemLoader().LoadFromDirectory(dataDirectory).ToDictionary(s => s.Code);
        _specials = new SpecialUnitLoader().LoadFromDirectory(dataDirectory).ToDictionary(s => s.Code);

        var map = new HexMap(0, MaxQ, 0, MaxR);
        _orchestrator = new AdvanceOrchestrator(
            new MovementSimulator(new PassabilityMap(map, [], [])),
            new CombatPhaseResolver(new BattleResolver(60), woundedPercent: 70),
            woundedPercent: 70,
            terrainAt: _ => TerrainType.Plains);

        _cases = BuildCases();
        BuildHud();
        LoadCase(0);

        // 헤드리스 자동 진행(예외 검증용): --combattestauto — 각 케이스를 몇 라운드 돌린다.
        if (OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).Contains("--combattestauto"))
        {
            var rounds = 0;
            var timer = new Godot.Timer { WaitTime = 0.3, Autostart = true };
            AddChild(timer);
            timer.Timeout += () =>
            {
                if (Ended() || rounds >= 8)
                {
                    var state = string.Join(" ", _units.Select(u => $"{Tag(u)}={u.Pool.Active}/{u.Pool.Wounded}"));
                    GD.Print($"[combattestauto] case {_caseIndex} after {rounds} rounds: {state}");
                    if (_caseIndex + 1 < _cases.Length)
                    {
                        LoadCase(_caseIndex + 1);
                        rounds = 0;
                    }
                    else
                    {
                        GD.Print("[combattestauto] all cases done");
                        timer.Stop();
                    }

                    return;
                }

                rounds++;
                OnStep();
            };
        }
    }

    // ── 케이스 정의 ──

    private CombatUnit Unit(int id, int owner, HexCoord pos, string templateCode, int troops,
        int might = 60, int intellect = 60, int atkBonus = 100, UnitCombatState? state = null)
    {
        var template = _templates[templateCode];
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos, 2, 2, 1,
            MovementDomain.Land, UnitMode.Attack, null, id);
        var stats = CombatStatsBuilder.BuildField(template, AptitudeGrade.A, 0, TerrainType.River, troops,
            atkBonusPercent: atkBonus);
        _tokenModel[id] = ModelIndex.GetValueOrDefault(templateCode, 0);
        return new CombatUnit(field, stats, new TroopPool(troops, 0),
            state ?? UnitCombatState.Create(intellect), might, intellect, MaxTroops: troops);
    }

    // 특수 유닛(판정 전환 반영): 등갑병 df→14 등. 토큰은 기반 병종 모델.
    private CombatUnit UnitSpecial(int id, int owner, HexCoord pos, string specialCode, int troops)
    {
        var special = _specials[specialCode];
        var baseTemplate = _templates[special.BaseCode];
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos, 2, 2, 1,
            MovementDomain.Land, UnitMode.Attack, null, id);
        var stats = CombatStatsBuilder.BuildFieldSpecial(special, baseTemplate, AptitudeGrade.A, 0,
            TerrainType.River, troops);
        _tokenModel[id] = ModelIndex.GetValueOrDefault(special.BaseCode, 0);
        return new CombatUnit(field, stats, new TroopPool(troops, 0),
            UnitCombatState.Create(60), 60, 60, MaxTroops: troops);
    }

    private CaseDef[] BuildCases() => new[]
    {
        new CaseDef("평타 1:1 (도검)", "도검병 A급 1만끼리 인접 교전. 진행마다 서로 760씩, 그중 70%는 부상.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(2, 1), "swordsman", 10000),
                Unit(2, 2, new HexCoord(3, 1), "swordsman", 10000),
            }),
        new CaseDef("병종 상성: 기병 vs 궁병", "기병(df12)은 튼튼하고 궁병(df8)은 약하다. 기병이 덜 맞고 더 때려 우위. 비용=성능.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(2, 1), "cavalry", 10000),
                Unit(2, 2, new HexCoord(3, 1), "archer", 10000),
            }),
        new CaseDef("최종병기: 상병 vs 도검", "상병(지수 196)이 도검(80)을 압도. 더 때리고 훨씬 덜 맞는다.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(2, 1), "war_elephant", 10000),
                Unit(2, 2, new HexCoord(3, 1), "swordsman", 10000),
            }),
        new CaseDef("해상: 거북선 vs 대선", "거북선(16/16)이 대선(12/10)을 압도하는 해상 최종병기.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(2, 1), "turtleship", 10000),
                Unit(2, 2, new HexCoord(3, 1), "large_ship", 10000),
            }),
        new CaseDef("다대일 포위: 도검 3 vs 상병 1", "도검 셋이 상병 하나를 포위. 상병 반격은 주대상 100%/나머지 60%로 갈려 셋을 다 못 막는다.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(2, 1), "swordsman", 10000),
                Unit(2, 1, new HexCoord(4, 1), "swordsman", 10000),
                Unit(3, 1, new HexCoord(3, 0), "swordsman", 10000),
                Unit(4, 2, new HexCoord(3, 1), "war_elephant", 10000),
            }),
        new CaseDef("무쌍 발동(무력 80)", "선봉 무쌍 게이지가 준비된 A1이 대체 공격으로 E1에 1459. 발동 후 초기화 → 5진행 뒤 재발동.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(2, 1), "swordsman", 10000, might: 80,
                    state: UnitCombatState.Create(60, vanguardActive: _actives["peerless"]).AdvanceField(5)),
                Unit(2, 2, new HexCoord(3, 1), "swordsman", 10000),
            }),
        new CaseDef("철벽 방어(무력 80)", "E1이 무쌍으로 치지만 A1 철벽이 받는 피해를 64%로 줄인다. 방어 액티브의 값어치.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(2, 1), "swordsman", 10000, might: 80,
                    state: UnitCombatState.Create(60, vanguardActive: _actives["iron_wall"]).AdvanceField(5)),
                Unit(2, 2, new HexCoord(3, 1), "swordsman", 10000, might: 80,
                    state: UnitCombatState.Create(60, vanguardActive: _actives["peerless"]).AdvanceField(5)),
            }),
        new CaseDef("특수유닛: 등갑병(df 14) vs 궁병", "등갑병은 df가 상병 판정(14)으로 격상돼 궁병 상대로 매우 튼튼하다.",
            () => new[]
            {
                UnitSpecial(1, 1, new HexCoord(2, 1), "deunggap", 10000),
                Unit(2, 2, new HexCoord(3, 1), "archer", 10000),
            }),
        new CaseDef("낙뢰 계략(예약 발동)", "A1이 예약한 낙뢰가 발동일 도달 → E1 병력 25%(2500) 즉발. 발동일 A1은 공격 안 함, 모략력 45 소비.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(2, 1), "swordsman", 10000,
                    state: UnitCombatState.Create(60, masteryPoints: 285)
                        .ReserveStratagem(_stratagems["lightning"], new UnitId(2))
                        .AdvanceField(2)),
                Unit(2, 2, new HexCoord(3, 1), "swordsman", 10000),
            }),
    };

    // ── 진행 ──

    private void OnStep()
    {
        if (Ended())
        {
            AppendLog("전투 종료.");
            UpdateButtons();
            return;
        }

        var before = _units.ToDictionary(u => u.Id.Value);
        var turn = _orchestrator.Run(_units);
        _units = turn.Units.ToList();

        foreach (var u in _units)
        {
            var pre = before[u.Id.Value];
            var lost = pre.Pool.Active - u.Pool.Active;

            // 발동 감지: 게이지 준비 해제 = 액티브 발동, 예약 사라짐 = 계략 처리
            if (pre.State.VanguardGauge.IsReady && !u.State.VanguardGauge.IsReady)
            {
                AppendLog($"{Tag(u)} 액티브 발동!");
            }
            if (pre.State.Reservation is not null && u.State.Reservation is null)
            {
                AppendLog($"{Tag(u)} 계략 발동!");
            }

            if (lost > 0)
            {
                AppendLog($"{Tag(u)} 병력 −{lost} (부상 {u.Pool.Wounded})");
            }

            RefreshToken(u);
            if (turn.Combat is not null && u.Pool.Active > 0)
            {
                _tokens[u.Id.Value].PlayAttackMotion();
            }
        }

        UpdateButtons();
    }

    private bool Ended()
    {
        var owners = _units.Where(u => u.Pool.Active > 0).Select(u => u.Field.Owner.Value).Distinct().Count();
        return owners < 2;
    }

    private void RefreshToken(CombatUnit u)
    {
        var alive = u.Pool.Active > 0;
        _troopLabels[u.Id.Value].Text = alive
            ? $"{u.Pool.Active}\n(부상 {u.Pool.Wounded})"
            : "전멸";
        _troopLabels[u.Id.Value].Modulate = alive ? new Color(0.97f, 0.96f, 0.92f) : new Color(0.9f, 0.4f, 0.35f);
        _tokens[u.Id.Value].DisplayStepTo(u.Field.Position, 0.2f);
    }

    private static string Tag(CombatUnit u) => (u.Field.Owner.Value == 1 ? "A" : "E") + u.Id.Value;

    // ── 셋업/토큰 ──

    private void LoadCase(int index)
    {
        _caseIndex = index;
        var def = _cases[index];

        foreach (var node in _spawned)
        {
            node.QueueFree();
        }

        _spawned.Clear();
        _tokens.Clear();
        _troopLabels.Clear();
        _tokenModel.Clear();
        _logLines.Clear();
        _units = def.Build().ToList();

        foreach (var u in _units)
        {
            SpawnToken(u);
        }

        // 서로 마주 보게
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
        AppendLog("진행을 눌러 한 라운드씩 교전을 재생하세요.");

        _camera.Setup(_view.HexToWorld(new HexCoord(MaxQ / 2, MaxR / 2)), MaxQ * 0.9f + 3f);
        UpdateButtons();
    }

    private void SpawnToken(CombatUnit u)
    {
        var color = u.Field.Owner.Value == 1 ? Blue : Red;
        var ctrl = new UnitController3D();
        AddChild(ctrl);
        _spawned.Add(ctrl);
        ctrl.InitDisplay(_view, color, _tokenModel.GetValueOrDefault(u.Id.Value, SwordsmanTroopIndex), u.Field.Position);

        var name = new Label3D
        {
            Text = Tag(u),
            Font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf"),
            FontSize = 84,
            PixelSize = 0.0022f,
            OutlineSize = 24,
            OutlineModulate = new Color(0f, 0f, 0f, 0.85f),
            Modulate = new Color(0.97f, 0.96f, 0.92f),
            Position = new Vector3(0f, 0.56f, 0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
        };
        ctrl.AddChild(name);

        var troops = new Label3D
        {
            Text = $"{u.Pool.Active}\n(부상 0)",
            Font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf"),
            FontSize = 72,
            PixelSize = 0.0020f,
            OutlineSize = 22,
            OutlineModulate = new Color(0f, 0f, 0f, 0.85f),
            Modulate = new Color(0.97f, 0.96f, 0.92f),
            Position = new Vector3(0f, 0.42f, 0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        ctrl.AddChild(troops);

        _tokens[u.Id.Value] = ctrl;
        _troopLabels[u.Id.Value] = troops;
    }

    // ── HUD ──

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var panel = new PanelContainer { Position = new Vector2(16, 16), CustomMinimumSize = new Vector2(560, 0) };
        layer.AddChild(panel);
        var box = new VBoxContainer();
        panel.AddChild(box);

        _titleLabel = new Label { Text = "" };
        _titleLabel.AddThemeFontSizeOverride("font_size", 22);
        box.AddChild(_titleLabel);

        _noteLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _noteLabel.CustomMinimumSize = new Vector2(540, 0);
        box.AddChild(_noteLabel);

        var buttons = new HBoxContainer();
        box.AddChild(buttons);
        _stepButton = new Button { Text = "진행 ▶" };
        _stepButton.Pressed += OnStep;
        buttons.AddChild(_stepButton);
        _caseButton = new Button { Text = "케이스 ▶▶" };
        _caseButton.Pressed += () => LoadCase((_caseIndex + 1) % _cases.Length);
        buttons.AddChild(_caseButton);

        _logLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _logLabel.CustomMinimumSize = new Vector2(540, 0);
        box.AddChild(_logLabel);
    }

    private void AppendLog(string line)
    {
        _logLines.Add(line);
        while (_logLines.Count > 10)
        {
            _logLines.RemoveAt(0);
        }

        _logLabel.Text = string.Join("\n", _logLines);
    }

    private void UpdateButtons()
    {
        _stepButton.Disabled = Ended();
    }
}
