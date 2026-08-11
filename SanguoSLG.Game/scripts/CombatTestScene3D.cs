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
/// Core <see cref="AdvanceOrchestrator"/>가 한 "진행"(이동 → 계략 → 전투 페이즈 → 정산)을 계산하면,
/// 토큰을 옮기고 병종별 공격 모션을 재생하며 전투 결과를 표에 한 행씩 쌓는다. 전투가 없는 진행은
/// "없음"으로 표기한다. 각 유닛에 패시브 1·액티브 1을 붙인다(계략은 대기). 규칙·수치는 Core 소유.
/// </summary>
public partial class CombatTestScene3D : Node3D
{
    private static readonly Color Blue = new(0.24f, 0.44f, 0.86f);
    private static readonly Color Red = new(0.82f, 0.22f, 0.18f);
    private const int MaxQ = 10;
    private const int MaxR = 2;

    // 패시브는 상시형만 써서 조건 없이 가산 버킷에 들어가게 한다(무조건 3단계).
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

    private CaseDef[] _cases = System.Array.Empty<CaseDef>();
    private int _caseIndex;
    private int _round;
    private List<CombatUnit> _units = new();
    private readonly Dictionary<int, UnitController3D> _tokens = new();
    private readonly Dictionary<int, Label3D> _troopLabels = new();
    private readonly Dictionary<int, int> _tokenModel = new();
    private readonly List<Node3D> _spawned = new();

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

        var map = new HexMap(0, MaxQ, 0, MaxR);
        _orchestrator = new AdvanceOrchestrator(
            new MovementSimulator(new PassabilityMap(map, [], [])),
            new CombatPhaseResolver(new BattleResolver(60), woundedPercent: 70),
            woundedPercent: 70,
            terrainAt: _ => TerrainType.Plains);

        _cases = BuildCases();
        BuildHud();
        LoadCase(0);

        // 헤드리스 자동 진행(예외 검증용): --combattestauto.
        if (OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).Contains("--combattestauto"))
        {
            var rounds = 0;
            var timer = new Godot.Timer { WaitTime = 0.3, Autostart = true };
            AddChild(timer);
            timer.Timeout += () =>
            {
                if (Ended() || rounds >= 12)
                {
                    var state = string.Join(" ", _units.Select(u => $"{Tag(u)}={u.Pool.Active}"));
                    GD.Print($"[combattestauto] case {_caseIndex} after {rounds}: {state}");
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

    // ── 케이스 ──

    // 유닛 하나: 병종 + 목표/모드 + 패시브 1 + 액티브 1. 계략은 대기(예약 안 함).
    private CombatUnit Unit(int id, int owner, HexCoord pos, string templateCode, HexCoord? target, UnitMode mode,
        string passiveCode, string activeCode, int might = 60, int intellect = 60, int troops = 10000)
    {
        var (atk, df) = PassiveBucketEvaluator.Evaluate(new[] { (_passives[passiveCode], 3) }, MeleeCtx);
        var stats = CombatStatsBuilder.BuildField(_templates[templateCode], AptitudeGrade.A, 0, TerrainType.River,
            troops, atkBonusPercent: atk, dfBonusPercent: df);
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos, 2, 2, 1,
            MovementDomain.Land, mode, target, id);
        _tokenModel[id] = ModelIndex.GetValueOrDefault(templateCode, 0);
        var state = UnitCombatState.Create(intellect, vanguardActive: _actives[activeCode]);
        return new CombatUnit(field, stats, new TroopPool(troops, 0), state, might, intellect, MaxTroops: troops);
    }

    private CaseDef[] BuildCases() => new[]
    {
        new CaseDef("진격 조우 → 소모전",
            "A1(공격)이 동진해 정지한 E1을 탐지·추격·정지 → 교전. 이후 진행마다 소모. A1=맹공+무쌍, E1=견수+철벽.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(0, 1), "swordsman", new HexCoord(10, 1), UnitMode.Attack, "fierce_assault", "peerless", might: 80),
                Unit(2, 2, new HexCoord(7, 1), "swordsman", null, UnitMode.Advance, "steadfast_guard", "iron_wall", might: 80),
            }),
        new CaseDef("전진 직행(무전투)",
            "A1(전진)은 길목의 E1(행군)을 무시하고 목표로 직행 → 조우 없이 도달. 표에 '없음'이 이어진다.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(0, 1), "swordsman", new HexCoord(10, 1), UnitMode.Advance, "fierce_assault", "peerless"),
                Unit(2, 2, new HexCoord(6, 0), "swordsman", null, UnitMode.March, "steadfast_guard", "iron_wall"),
            }),
        new CaseDef("정면 조우 교전",
            "A1·E1이 서로 목표로 마주 진격 → 가운데서 정지 → 대칭 소모. 둘 다 맹공+무쌍.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(0, 1), "swordsman", new HexCoord(10, 1), UnitMode.Attack, "fierce_assault", "peerless", might: 80),
                Unit(2, 2, new HexCoord(10, 1), "swordsman", new HexCoord(0, 1), UnitMode.Attack, "fierce_assault", "peerless", might: 80),
            }),
        new CaseDef("다대일 협격(이동 포위)",
            "A1·A2가 양쪽에서 중앙의 E1(상병)로 진격·포위. E1 반격은 주100/부60로 갈려 둘을 못 막는다.",
            () => new[]
            {
                Unit(1, 1, new HexCoord(0, 1), "swordsman", new HexCoord(4, 1), UnitMode.Attack, "fierce_assault", "peerless"),
                Unit(2, 1, new HexCoord(10, 1), "swordsman", new HexCoord(6, 1), UnitMode.Attack, "fierce_assault", "peerless"),
                Unit(4, 2, new HexCoord(5, 1), "war_elephant", null, UnitMode.Advance, "steadfast_guard", "iron_wall"),
            }),
    };

    // ── 진행 ──

    private void OnStep()
    {
        if (Ended())
        {
            AddRow("종료", "");
            UpdateButtons();
            return;
        }

        _round++;
        var before = _units.ToDictionary(u => u.Id.Value);
        var turn = _orchestrator.Run(_units);
        _units = turn.Units.ToList();

        var parts = new List<string>();
        foreach (var u in _units)
        {
            var pre = before[u.Id.Value];

            var fired = pre.State.VanguardGauge.IsReady && !u.State.VanguardGauge.IsReady ? "★" : "";
            var lost = pre.Pool.Active - u.Pool.Active;
            if (lost > 0)
            {
                parts.Add($"{Tag(u)}{fired} −{lost}");
            }
            else if (fired != "")
            {
                parts.Add($"{Tag(u)}★발동");
            }

            // 토큰 이동 + 마주 보기 + 병종 공격 모션
            _tokens[u.Id.Value].DisplayStepTo(u.Field.Position, 0.25f);
            var foe = _units.FirstOrDefault(o => o.Field.Owner != u.Field.Owner && o.Pool.Active > 0);
            if (foe is not null)
            {
                _tokens[u.Id.Value].FaceToward(_view.HexToWorld(foe.Field.Position));
            }

            RefreshLabel(u);
            if (turn.Combat is not null && lost >= 0 && u.Pool.Active > 0 && parts.Any(p => p.StartsWith(Tag(u))))
            {
                _tokens[u.Id.Value].PlayAttackMotion();
            }
        }

        AddRow($"{_round}", parts.Count > 0 ? string.Join("  ·  ", parts) : "없음");
        UpdateButtons();
    }

    private bool Ended()
        => _units.Where(u => u.Pool.Active > 0).Select(u => u.Field.Owner.Value).Distinct().Count() < 2;

    private void RefreshLabel(CombatUnit u)
    {
        var alive = u.Pool.Active > 0;
        _troopLabels[u.Id.Value].Text = alive ? $"{u.Pool.Active}\n(부상 {u.Pool.Wounded})" : "전멸";
        _troopLabels[u.Id.Value].Modulate = alive ? new Color(0.97f, 0.96f, 0.92f) : new Color(0.9f, 0.4f, 0.35f);
    }

    private static string Tag(CombatUnit u) => (u.Field.Owner.Value == 1 ? "A" : "E") + u.Id.Value;

    // ── 셋업/토큰 ──

    private void LoadCase(int index)
    {
        _caseIndex = index;
        _round = 0;
        var def = _cases[index];

        foreach (var node in _spawned)
        {
            node.QueueFree();
        }

        _spawned.Clear();
        _tokens.Clear();
        _troopLabels.Clear();
        _tokenModel.Clear();
        _units = def.Build().ToList();

        ClearTable();
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

        _camera.Setup(_view.HexToWorld(new HexCoord(MaxQ / 2, MaxR / 2)), MaxQ * 0.72f + 4f);
        UpdateButtons();
    }

    private void SpawnToken(CombatUnit u)
    {
        var color = u.Field.Owner.Value == 1 ? Blue : Red;
        var ctrl = new UnitController3D();
        AddChild(ctrl);
        _spawned.Add(ctrl);
        ctrl.InitDisplay(_view, color, _tokenModel.GetValueOrDefault(u.Id.Value, 0), u.Field.Position);

        ctrl.AddChild(MakeLabel(Tag(u), 84, 0.56f));
        var troops = MakeLabel($"{u.Pool.Active}\n(부상 0)", 72, 0.42f);
        troops.HorizontalAlignment = HorizontalAlignment.Center;
        ctrl.AddChild(troops);

        _tokens[u.Id.Value] = ctrl;
        _troopLabels[u.Id.Value] = troops;
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

    // ── HUD (결과 표) ──

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var panel = new PanelContainer { Position = new Vector2(16, 16), CustomMinimumSize = new Vector2(600, 0) };
        layer.AddChild(panel);
        var box = new VBoxContainer();
        panel.AddChild(box);

        _titleLabel = new Label { Text = "" };
        _titleLabel.AddThemeFontSizeOverride("font_size", 22);
        box.AddChild(_titleLabel);

        _noteLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _noteLabel.CustomMinimumSize = new Vector2(580, 0);
        box.AddChild(_noteLabel);

        var buttons = new HBoxContainer();
        box.AddChild(buttons);
        _stepButton = new Button { Text = "진행 ▶" };
        _stepButton.Pressed += OnStep;
        buttons.AddChild(_stepButton);
        _caseButton = new Button { Text = "케이스 ▶▶" };
        _caseButton.Pressed += () => LoadCase((_caseIndex + 1) % _cases.Length);
        buttons.AddChild(_caseButton);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(580, 340) };
        box.AddChild(scroll);
        _table = new GridContainer { Columns = 2 };
        scroll.AddChild(_table);
    }

    private void ClearTable()
    {
        foreach (var child in _table.GetChildren())
        {
            child.QueueFree();
        }

        AddRow("진행", "전투 결과", header: true);
    }

    private void AddRow(string a, string b, bool header = false)
    {
        _table.AddChild(Cell(a, header, 90));
        _table.AddChild(Cell(b, header, 470));
    }

    private static Label Cell(string text, bool header, int width)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(width, 0),
        };
        if (header)
        {
            label.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 1f));
        }

        return label;
    }

    private void UpdateButtons()
    {
        _stepButton.Disabled = Ended();
    }
}
