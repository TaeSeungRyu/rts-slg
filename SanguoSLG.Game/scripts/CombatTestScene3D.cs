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
/// 패시브 1·액티브 1을 붙인다(계략은 대기). 규칙·수치는 Core 소유.
/// </summary>
public partial class CombatTestScene3D : Node3D
{
    private static readonly Color Blue = new(0.24f, 0.44f, 0.86f);
    private static readonly Color Red = new(0.82f, 0.22f, 0.18f);
    private const int MaxQ = 10;
    private const int MaxR = 2;

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
    private readonly List<int> _orderedIds = new();
    private readonly Dictionary<int, UnitController3D> _tokens = new();
    private readonly Dictionary<int, Label3D> _troopLabels = new();
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
    };

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
        if (_pending.Combat is not null)
        {
            _beats.Enqueue(PlayAttacks);
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
        _units = turn.Units.ToList();

        foreach (var u in _units)
        {
            RefreshLabel(u);
        }

        AddResultRow(turn);
        _pending = null;
    }

    private bool Ended()
        => _units.Where(u => u.Pool.Active > 0).Select(u => u.Field.Owner.Value).Distinct().Count() < 2;

    private void RefreshLabel(CombatUnit u)
    {
        var alive = u.Pool.Active > 0;
        _troopLabels[u.Id.Value].Text = alive ? $"{u.Pool.Active}/{u.MaxTroops}" : "전멸";
        _troopLabels[u.Id.Value].Modulate = alive ? new Color(0.97f, 0.96f, 0.92f) : new Color(0.9f, 0.4f, 0.35f);
    }

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
        _tokenModel.Clear();
        _tokenHex.Clear();
        _units = def.Build().ToList();

        _orderedIds.Clear();
        _orderedIds.AddRange(_units
            .OrderBy(u => u.Field.Owner.Value).ThenBy(u => u.Id.Value)
            .Select(u => u.Id.Value));

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
        _camera.Setup(_view.HexToWorld(new HexCoord(MaxQ / 2, MaxR / 2)), MaxQ * 0.72f + 4f);
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

        ctrl.AddChild(MakeLabel(Tag(u), 84, 0.56f));
        var troops = MakeLabel($"{u.Pool.Active}/{u.MaxTroops}", 66, 0.42f);
        troops.HorizontalAlignment = HorizontalAlignment.Center;
        ctrl.AddChild(troops);

        _tokens[u.Id.Value] = ctrl;
        _troopLabels[u.Id.Value] = troops;
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

    private void BuildTableHeader()
    {
        foreach (var child in _table.GetChildren())
        {
            child.QueueFree();
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

        foreach (var id in _orderedIds)
        {
            var u = _units.First(x => x.Id.Value == id);
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

            _table.AddChild(Cell(string.Join("\n", lines), header: false, width: 150));
        }
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
