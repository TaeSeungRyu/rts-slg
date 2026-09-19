using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 액티브 스킬만 빠르게 반복 검수하는 독립 하베스트. 캠페인 저장 상태와 무관하게 제갈량 1부대와
/// 정지 적군 5부대를 10,000명으로 배치하고, 선택 스킬을 5일 충전한 뒤 실제 Core 전투를 실행한다.
/// </summary>
public partial class ActiveEffectTestScene3D : Node3D
{
    private const int AllyId = 1;
    private static readonly Color AllyColor = new("#3e78c4");
    private static readonly Color EnemyColor = new("#b8423c");

    private MapView3D _view = null!;
    private CameraController3D _camera = null!;
    private Dictionary<string, ActiveSkill> _actives = null!;
    private TroopTemplate _template = null!;
    private General _zhugeLiang = null!;
    private AdvanceOrchestrator _orchestrator = null!;
    private readonly Dictionary<int, UnitController3D> _tokens = new();
    private readonly Dictionary<int, Label3D> _troopLabels = new();
    private readonly Dictionary<int, ActiveSkillGaugeView3D> _gauges = new();
    private List<CombatUnit> _units = [];
    private OptionButton _skillSelect = null!;
    private Button _advanceButton = null!;
    private Label _summary = null!;
    private RichTextLabel _log = null!;
    private int _round;
    private int _lastEffectCount;
    private int _advanceCount;
    private int _allyActiveFireCount;
    private int _allyActiveFireDay;
    private ActiveSkillChargeView3D? _chargeView;
    private bool _presentationRunning;
    private int _presentationGeneration;

    public void Build(MapView3D view, CameraController3D camera, string dataDirectory)
    {
        _view = view;
        _camera = camera;
        _actives = new ActiveSkillLoader().LoadFromDirectory(dataDirectory).ToDictionary(x => x.Code);
        _template = new TroopTypeLoader().LoadFromDirectory(dataDirectory).First(x => x.Code == "swordsman");
        _zhugeLiang = new GeneralLoader().LoadFromDirectory(dataDirectory).First(x => x.Name == "제갈량");

        var map = new HexMap(0, 11, 0, 6);
        _orchestrator = new AdvanceOrchestrator(
            new MovementSimulator(new PassabilityMap(map, [], [])),
            new CombatPhaseResolver(new BattleResolver(60), woundedPercent: 70),
            woundedPercent: 70,
            terrainAt: map.TerrainAt);

        BuildHud();
        ResetScenario();
        _camera.Setup(_view.HexToWorld(new HexCoord(5, 3)), 9f);

        var args = OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).ToHashSet();
        if (args.Contains("--activeeffecttestresetqa"))
        {
            CallDeferred(MethodName.RunResetQa);
        }
        else if (args.Contains("--activeeffecttestpresentqa"))
        {
            CallDeferred(MethodName.RunPresentationQa);
        }
        else if (args.Contains("--activeeffecttestauto"))
        {
            CallDeferred(MethodName.RunAutoQa);
        }
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var left = new PanelContainer
        {
            Position = new Vector2(16, 16),
            CustomMinimumSize = new Vector2(420, 620),
        };
        layer.AddChild(left);
        var leftBox = new VBoxContainer();
        left.AddChild(leftBox);
        leftBox.AddChild(Heading("전투 효과 로그"));
        _log = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            CustomMinimumSize = new Vector2(390, 550),
        };
        leftBox.AddChild(_log);

        var right = new PanelContainer
        {
            AnchorLeft = 1f,
            AnchorRight = 1f,
            OffsetLeft = -430,
            OffsetRight = -16,
            OffsetTop = 16,
            CustomMinimumSize = new Vector2(414, 0),
        };
        layer.AddChild(right);
        var box = new VBoxContainer();
        right.AddChild(box);
        box.AddChild(Heading("액티브 스킬 검수장"));
        box.AddChild(new Label { Text = "아군: 제갈량 10,000  |  적군: 5부대 × 10,000" });
        box.AddChild(new Label { Text = "스킬을 바꾸면 전장이 초기화됩니다. 진행 1회 = 7일 교전입니다." });
        _skillSelect = new OptionButton { CustomMinimumSize = new Vector2(380, 42) };
        foreach (var skill in _actives.Values.OrderBy(x => x.Type).ThenBy(x => x.Name))
        {
            _skillSelect.AddItem($"{skill.Name} · {TypeName(skill.Type)}");
            _skillSelect.SetItemMetadata(_skillSelect.ItemCount - 1, skill.Code);
        }
        var fireIndex = Enumerable.Range(0, _skillSelect.ItemCount)
            .First(i => _skillSelect.GetItemMetadata(i).AsString() == "fire_plot");
        _skillSelect.Select(fireIndex);
        _skillSelect.ItemSelected += _ => ResetScenario();
        box.AddChild(_skillSelect);

        _advanceButton = new Button { Text = "진행 · 7일 교전 ▶", CustomMinimumSize = new Vector2(380, 52) };
        _advanceButton.Pressed += BeginSevenDayPresentation;
        box.AddChild(_advanceButton);
        var resetButton = new Button { Text = "초기화 ↺", CustomMinimumSize = new Vector2(380, 42) };
        resetButton.Pressed += ResetScenario;
        box.AddChild(resetButton);
        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(380, 220) };
        box.AddChild(_summary);
    }

    private static Label Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 24);
        return label;
    }

    private void ResetScenario()
    {
        _presentationGeneration++;
        var charge = _chargeView;
        _chargeView = null;
        if (GodotObject.IsInstanceValid(charge) && !charge!.IsQueuedForDeletion()) charge.QueueFree();
        foreach (var token in _tokens.Values) token.QueueFree();
        _tokens.Clear();
        _troopLabels.Clear();
        _gauges.Clear();
        _round = 0;
        _advanceCount = 0;
        _lastEffectCount = 0;
        _allyActiveFireCount = 0;
        _allyActiveFireDay = 0;
        _presentationRunning = false;
        if (_advanceButton is not null) _advanceButton.Disabled = false;
        if (_skillSelect is not null) _skillSelect.Disabled = false;

        var selected = SelectedSkill();
        _units =
        [
            MakeUnit(AllyId, 1, new HexCoord(4, 3), 100, selected),
            MakeUnit(11, 2, new HexCoord(5, 1), 62),
            MakeUnit(12, 2, new HexCoord(5, 2), 66),
            MakeUnit(13, 2, new HexCoord(5, 3), 70),
            MakeUnit(14, 2, new HexCoord(5, 4), 74),
            MakeUnit(15, 2, new HexCoord(5, 5), 78),
        ];
        foreach (var unit in _units) Spawn(unit);
        FaceOpponents();
        _log.Text = $"[b]전장 초기화[/b]\n제갈량 액티브: {selected.Name}\n적군은 이동하지 않으며 전투·피해 계산은 실제 Core 규칙을 사용합니다.\n";
        RefreshSummary(null);
    }

    private CombatUnit MakeUnit(int id, int owner, HexCoord at, int intellect, ActiveSkill? active = null, int troops = 10000)
    {
        var stats = CombatStatsBuilder.BuildField(_template, AptitudeGrade.A, 0, TerrainType.River, troops);
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), at,
            _template.MovementPerDay, _template.Detection, _template.RangeUnit,
            MovementDomain.Land, UnitMode.Advance, null, id, _template.RangeCastle);
        return new CombatUnit(field, stats, new TroopPool(troops, 0),
            UnitCombatState.Create(intellect, active), owner == 1 ? _zhugeLiang.Might : 70,
            intellect, troops, _template.Class, TroopCode: _template.Code,
            VanguardId: owner == 1 ? _zhugeLiang.Id : null);
    }

    private ActiveSkill SelectedSkill()
        => _actives[_skillSelect.GetItemMetadata(_skillSelect.Selected).AsString()];

    private void BeginSevenDayPresentation()
    {
        if (_presentationRunning) return;
        _presentationRunning = true;
        _advanceButton.Disabled = true;
        _skillSelect.Disabled = true;
        if (_tokens.TryGetValue(AllyId, out var caster))
        {
            _chargeView = new ActiveSkillChargeView3D();
            caster.AddChild(_chargeView);
        }
        AppendLog("[color=#ffd05a]1~5일차 · 액티브 준비 중…[/color]");
        BeginAdvanceBatch();
        RunPresentedDay(1, _presentationGeneration);
    }

    private void AdvanceSevenDays()
    {
        if (_units.All(x => x.Field.Owner.Value != 1) || _units.All(x => x.Field.Owner.Value != 2))
        {
            AppendLog("[color=yellow]전투 종료 — 스킬을 다시 선택해 초기화하세요.[/color]");
            return;
        }

        BeginAdvanceBatch();
        AdvanceTurn? lastTurn = null;
        for (var day = 1; day <= 7; day++)
        {
            lastTurn = AdvanceOneDay(day);
            if (lastTurn is null) break;
        }
        RefreshSummary(lastTurn);
    }

    private void BeginAdvanceBatch()
    {
        _advanceCount++;
        _lastEffectCount = 0;
        AppendLog($"\n[font_size=20][b]진행 {_advanceCount} · 7일 교전 시작[/b][/font_size]");
    }

    private void RunPresentedDay(int day, int generation)
    {
        if (generation != _presentationGeneration) return;
        if (day > 7 || AdvanceOneDay(day) is null)
        {
            RefreshSummary(null);
            _presentationRunning = false;
            _advanceButton.Disabled = false;
            _skillSelect.Disabled = false;
            return;
        }
        RefreshSummary(null);
        var timer = GetTree().CreateTimer(0.72);
        timer.Timeout += () => RunPresentedDay(day + 1, generation);
    }

    private AdvanceTurn? AdvanceOneDay(int day)
    {
        if (_units.All(x => x.Field.Owner.Value != 1) || _units.All(x => x.Field.Owner.Value != 2)) return null;
        _round++;
        var before = _units.ToDictionary(x => x.Id, x => x.Pool.Active);
        var turn = _orchestrator.Run(_units, maxDays: 1);
        _units = turn.Units.ToList();

        foreach (var (uid, dealt) in turn.Combat?.DamageDealt ?? new Dictionary<UnitId, int>())
            if (dealt > 0 && _tokens.TryGetValue(uid.Value, out var attacker)) attacker.PlayAttackMotion();
        RefreshTokens();
        AppendLog($"[b]{day}일차[/b]");
        foreach (var unit in _units.OrderBy(x => x.Id.Value))
        {
            var normalTaken = turn.Combat?.DamageTaken.GetValueOrDefault(unit.Id) ?? 0;
            var skillTaken = turn.StratagemDamage.GetValueOrDefault(unit.Id) + turn.StatusDamage.GetValueOrDefault(unit.Id);
            var loss = before.GetValueOrDefault(unit.Id) - unit.Pool.Active;
            AppendLog($"{Tag(unit)} 일반 −{normalTaken:N0} | 스킬 −{skillTaken:N0} | 총감소 −{loss:N0} | 잔여 {unit.Pool.Active:N0}");
        }
        if (turn.FiredActives.TryGetValue(new UnitId(AllyId), out var fired))
        {
            _allyActiveFireCount++;
            _allyActiveFireDay = day;
            AppendLog($"[color=orange][b]제갈량 {fired.Name} 발동[/b][/color]");
            ActiveSkillPresentation.ShowBanner(this, "제갈량", fired);
            _chargeView?.Complete();
            _chargeView = null;
        }
        _lastEffectCount += PlaySkillEffects(turn);
        return turn;
    }

    private void RefreshTokens()
    {
        foreach (var (id, token) in _tokens.ToList())
        {
            var unit = _units.FirstOrDefault(x => x.Id.Value == id);
            if (unit is null)
            {
                token.QueueFree();
                _tokens.Remove(id);
                _troopLabels.Remove(id);
                _gauges.Remove(id);
                continue;
            }
            _troopLabels[id].Text = unit.Pool.Active.ToString("N0");
            if (_gauges.TryGetValue(id, out var gauge)) gauge.SetSkill(unit.State.VanguardActive, unit.State.VanguardGauge);
        }
    }

    private int PlaySkillEffects(AdvanceTurn turn)
    {
        if (!turn.FiredActives.TryGetValue(new UnitId(AllyId), out var fired) || fired.Code != "fire_plot")
        {
            return 0;
        }

        var count = 0;
        foreach (var target in _units.Where(x => x.Field.Owner.Value == 2 && x.State.Statuses.Any(s => s.IsFire)))
        {
            if (!_tokens.TryGetValue(target.Id.Value, out var token)) continue;
            if (ActiveSkillPresentation.AttachEffect(token, fired)) count++;
        }
        AppendLog($"화계 연출: 적군 {count}부대에 빨강색 상승 화염 표시");
        return count;
    }

    private void Spawn(CombatUnit unit)
    {
        var token = new UnitController3D();
        AddChild(token);
        token.InitDisplay(_view, unit.Field.Owner.Value == 1 ? AllyColor : EnemyColor, 0, unit.Field.Position);
        var name = unit.Field.Owner.Value == 1 ? "제갈량" : $"적군 {unit.Id.Value - 10}";
        token.AddChild(MakeWorldLabel(name, 0.58f, 82));
        var troops = MakeWorldLabel(unit.Pool.Active.ToString("N0"), 0.34f, 68);
        token.AddChild(troops);
        _tokens[unit.Id.Value] = token;
        _troopLabels[unit.Id.Value] = troops;
        var gauge = new ActiveSkillGaugeView3D { Visible = unit.State.VanguardActive is not null };
        token.AddChild(gauge);
        gauge.SetSkill(unit.State.VanguardActive, unit.State.VanguardGauge);
        _gauges[unit.Id.Value] = gauge;
    }

    private static Label3D MakeWorldLabel(string text, float y, int size) => new()
    {
        Text = text,
        Font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf"),
        FontSize = size,
        PixelSize = 0.0021f,
        OutlineSize = 24,
        OutlineModulate = new Color(0f, 0f, 0f, 0.86f),
        Position = new Vector3(0f, y, 0f),
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        NoDepthTest = true,
    };

    private void FaceOpponents()
    {
        foreach (var unit in _units)
        {
            var opponent = _units.Where(x => x.Field.Owner != unit.Field.Owner)
                .OrderBy(x => x.Field.Position.Distance(unit.Field.Position)).First();
            _tokens[unit.Id.Value].FaceToward(_view.HexToWorld(opponent.Field.Position));
        }
    }

    private void RefreshSummary(AdvanceTurn? turn)
    {
        var selected = SelectedSkill();
        var fired = _allyActiveFireCount > 0 ? $"{_allyActiveFireDay}일차 발동 ({_allyActiveFireCount}회)" : "대기";
        var rows = _units.OrderBy(x => x.Id.Value).Select(x => $"{Tag(x),-8} {x.Pool.Active,6:N0}");
        _summary.Text = $"선택 스킬: {selected.Name}\n유형: {TypeName(selected.Type)}\n상태: {fired}\n\n병력 현황\n{string.Join("\n", rows)}";
    }

    private void AppendLog(string line)
    {
        _log.AppendText(line + "\n");
        _log.ScrollToLine(Math.Max(0, _log.GetLineCount() - 1));
    }

    private static string Tag(CombatUnit unit) => unit.Id.Value == AllyId ? "아군 제갈량" : $"적군 {unit.Id.Value - 10}";
    private static string TypeName(ActiveType type) => type switch
    {
        ActiveType.Strike => "공격형",
        ActiveType.Defense => "방어형",
        ActiveType.Heal => "회복형",
        ActiveType.Tactic => "계략형",
        _ => type.ToString(),
    };

    private void RunAutoQa()
    {
        _chargeView = new ActiveSkillChargeView3D();
        _tokens[AllyId].AddChild(_chargeView);
        AdvanceSevenDays();
        var ally = _units.FirstOrDefault(x => x.Id.Value == AllyId);
        var fired = ally is not null && _lastEffectCount > 0;
        var gaugePassed = _gauges.TryGetValue(AllyId, out var battleGauge)
            && battleGauge.SkillCode == "fire_plot" && battleGauge.FilledSegments == 1;
        GD.Print($"[activeeffecttestauto] units={_units.Count} enemy={_units.Count(x => x.Field.Owner.Value == 2)} skill={SelectedSkill().Code} fired={fired} fireDay={_allyActiveFireDay} fireCount={_allyActiveFireCount} effects={_lastEffectCount} days={_round} advances={_advanceCount}");
        var battlePassed = fired && gaugePassed && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount >= 1
            && _round == 7 && _advanceCount == 1 && ally?.MaxTroops == 10000;
        ResetScenario();
        var resetAlly = _units.FirstOrDefault(x => x.Id.Value == AllyId);
        var resetPassed = _units.Count == 6 && _units.Count(x => x.Field.Owner.Value == 2) == 5
            && _gauges.Count == 6 && resetAlly?.Pool.Active == 10000
            && _gauges[AllyId].SkillCode == "fire_plot" && _gauges[AllyId].FilledSegments == 0
            && _round == 0 && _advanceCount == 0 && _allyActiveFireCount == 0 && _allyActiveFireDay == 0;
        GD.Print($"[activeeffecttestauto] reset={resetPassed} units={_units.Count} ally={resetAlly?.Pool.Active} days={_round}");
        GetTree().Quit(battlePassed && resetPassed ? 0 : 1);
    }

    private void RunPresentationQa()
    {
        BeginSevenDayPresentation();
        var timer = GetTree().CreateTimer(6.2);
        timer.Timeout += () =>
        {
            var passed = !_presentationRunning && _round == 7 && _allyActiveFireDay == 6
                && _allyActiveFireCount == 1 && _gauges[AllyId].FilledSegments == 1;
            GD.Print($"[activeeffecttestpresentqa] passed={passed} days={_round} fireDay={_allyActiveFireDay} gauge={_gauges[AllyId].FilledSegments}");
            GetTree().Quit(passed ? 0 : 1);
        };
    }

    private void RunResetQa()
    {
        BeginSevenDayPresentation();
        var first = GetTree().CreateTimer(0.18);
        first.Timeout += () =>
        {
            ResetScenario();
            ResetScenario(); // 이미 해제된 준비 효과를 다시 해제해도 예외가 없어야 한다.
            var verify = GetTree().CreateTimer(1.0);
            verify.Timeout += () =>
            {
                var ally = _units.FirstOrDefault(x => x.Id.Value == AllyId);
                var passed = !_presentationRunning && _round == 0 && _advanceCount == 0
                    && _chargeView is null && ally?.Pool.Active == 10000 && !_advanceButton.Disabled && !_skillSelect.Disabled;
                GD.Print($"[activeeffecttestresetqa] passed={passed} days={_round} advances={_advanceCount} ally={ally?.Pool.Active}");
                GetTree().Quit(passed ? 0 : 1);
            };
        };
    }
}
