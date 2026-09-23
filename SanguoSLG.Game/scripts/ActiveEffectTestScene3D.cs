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
    // 보병 공격 최장 1회는 편대 산개(0.55)+전진(0.16)+타격(0.07)+멈춤(0.08)+복귀(0.28)=1.14초다.
    // 이보다 짧으면 다음 날 게이지만 오르고 PlayAttackMotion이 무시되므로 프레임 여유를 둔다.
    private const double NormalDayPresentationSeconds = 1.30;
    // 가장 긴 단발 효과(무쌍 1.55초)와 배너 전환을 잘리지 않고 확인할 최소 시간.
    private const double ActiveDayPresentationSeconds = 1.85;
    private const double AdjutantChainDelaySeconds = 0.20;
    private static readonly Color AllyColor = new("#3e78c4");
    private static readonly Color EnemyColor = new("#b8423c");

    private MapView3D _view = null!;
    private CameraController3D _camera = null!;
    private Dictionary<string, ActiveSkill> _actives = null!;
    private TroopTemplate _template = null!;
    private General _zhugeLiang = null!;
    private General _adjutant = null!;
    private AdvanceOrchestrator _orchestrator = null!;
    private readonly Dictionary<int, UnitController3D> _tokens = new();
    private readonly Dictionary<int, Label3D> _troopLabels = new();
    private readonly Dictionary<int, ActiveSkillGaugeView3D> _gauges = new();
    private List<CombatUnit> _units = [];
    private OptionButton _skillSelect = null!;
    private OptionButton _adjutantSkillSelect = null!;
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
    private int _chargeAppearedDay;
    private readonly List<string> _allyFiredSkillCodes = [];
    private int _extendedActiveTurns;
    private int _compressedAdjutantChains;
    private int _batchAllyActiveFireCount;

    public void Build(MapView3D view, CameraController3D camera, string dataDirectory)
    {
        _view = view;
        _camera = camera;
        _actives = new ActiveSkillLoader().LoadFromDirectory(dataDirectory).ToDictionary(x => x.Code);
        _template = new TroopTypeLoader().LoadFromDirectory(dataDirectory).First(x => x.Code == "swordsman");
        _zhugeLiang = new GeneralLoader().LoadFromDirectory(dataDirectory).First(x => x.Name == "제갈량");
        _adjutant = new GeneralLoader().LoadFromDirectory(dataDirectory).First(x => x.Name == "조운");

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
        if (args.Contains("--activeeffecttestironwallqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "iron_wall");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunIronWallQa);
        }
        else if (args.Contains("--activeeffecttestriposteqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "riposte");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunRiposteQa);
        }
        else if (args.Contains("--activeeffecttestturtleformationqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "turtle_formation");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunTurtleFormationQa);
        }
        else if (args.Contains("--activeeffecttestevasionqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "evasion");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunEvasionQa);
        }
        else if (args.Contains("--activeeffecttestholdthelineqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "hold_the_line");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunHoldTheLineQa);
        }
        else if (args.Contains("--activeeffecttestfieldmedicqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "field_medic");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunFieldMedicQa);
        }
        else if (args.Contains("--activeeffecttestregroupqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "regroup");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunRegroupQa);
        }
        else if (args.Contains("--activeeffecttestrallyqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "rally");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunRallyQa);
        }
        else if (args.Contains("--activeeffecttestresupplyqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "resupply");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunResupplyQa);
        }
        else if (args.Contains("--activeeffecttestsecondwindqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "second_wind");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunSecondWindQa);
        }
        else if (args.Contains("--activeeffecttestpatchqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "patch");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunPatchQa);
        }
        else if (args.Contains("--activeeffecttestlightningqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "lightning");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunLightningQa);
        }
        else if (args.Contains("--activeeffecttestconfoundqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "confound");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunConfoundQa);
        }
        else if (args.Contains("--activeeffecttestroutqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "rout");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunRoutQa);
        }
        else if (args.Contains("--activeeffecttestbraceqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "brace");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunBraceQa);
        }
        else if (args.Contains("--activeeffecttestdoublehitqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "double_hit");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunDoubleHitQa);
        }
        else if (args.Contains("--activeeffecttestheavyblowqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "heavy_blow");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunHeavyBlowQa);
        }
        else if (args.Contains("--activeeffecttestarmorbreakqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "armor_break");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunArmorBreakQa);
        }
        else if (args.Contains("--activeeffecttestchainstrikeqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "chain_strike");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunChainStrikeQa);
        }
        else if (args.Contains("--activeeffecttestcrushqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "crush");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunCrushQa);
        }
        else if (args.Contains("--activeeffecttesttigerstrikeqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "tiger_strike");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunTigerStrikeQa);
        }
        else if (args.Contains("--activeeffecttestbreakthroughqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "breakthrough");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunBreakthroughQa);
        }
        else if (args.Contains("--activeeffecttestreapqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "reap");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunAutoQa);
        }
        else if (args.Contains("--activeeffecttestbarrageqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "barrage");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunAutoQa);
        }
        else if (args.Contains("--activeeffecttestflashqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "flash");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunAutoQa);
        }
        else if (args.Contains("--activeeffecttesteffectsurvivesdeathqa"))
        {
            var vanguardIndex = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "one_man_army");
            var adjutantIndex = Enumerable.Range(0, _adjutantSkillSelect.ItemCount)
                .First(i => _adjutantSkillSelect.GetItemMetadata(i).AsString() == "peerless");
            _skillSelect.Select(vanguardIndex);
            _adjutantSkillSelect.Select(adjutantIndex);
            ResetScenario();
            CallDeferred(MethodName.RunEffectSurvivesDeathQa);
        }
        else if (args.Contains("--activeeffecttesttwopresentationqa"))
        {
            var vanguardIndex = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "one_man_army");
            var adjutantIndex = Enumerable.Range(0, _adjutantSkillSelect.ItemCount)
                .First(i => _adjutantSkillSelect.GetItemMetadata(i).AsString() == "peerless");
            _skillSelect.Select(vanguardIndex);
            _adjutantSkillSelect.Select(adjutantIndex);
            ResetScenario();
            CallDeferred(MethodName.RunTwoPresentationQa);
        }
        else if (args.Contains("--activeeffecttestsecondadvancelethalqa"))
        {
            var vanguardIndex = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "one_man_army");
            var adjutantIndex = Enumerable.Range(0, _adjutantSkillSelect.ItemCount)
                .First(i => _adjutantSkillSelect.GetItemMetadata(i).AsString() == "peerless");
            _skillSelect.Select(vanguardIndex);
            _adjutantSkillSelect.Select(adjutantIndex);
            ResetScenario();
            CallDeferred(MethodName.RunSecondAdvanceLethalQa);
        }
        else if (args.Contains("--activeeffecttestlethaladjutantqa"))
        {
            var vanguardIndex = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "iron_wall");
            var adjutantIndex = Enumerable.Range(0, _adjutantSkillSelect.ItemCount)
                .First(i => _adjutantSkillSelect.GetItemMetadata(i).AsString() == "one_man_army");
            _skillSelect.Select(vanguardIndex);
            _adjutantSkillSelect.Select(adjutantIndex);
            ResetScenario();
            CallDeferred(MethodName.RunLethalAdjutantEffectQa);
        }
        else if (args.Contains("--activeeffecttestadjutantpresentqa"))
        {
            var index = Enumerable.Range(0, _adjutantSkillSelect.ItemCount)
                .First(i => _adjutantSkillSelect.GetItemMetadata(i).AsString() == "peerless");
            _adjutantSkillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunAdjutantPresentationQa);
        }
        else if (args.Contains("--activeeffecttestadjutantqa"))
        {
            var index = Enumerable.Range(0, _adjutantSkillSelect.ItemCount)
                .First(i => _adjutantSkillSelect.GetItemMetadata(i).AsString() == "peerless");
            _adjutantSkillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunAdjutantQa);
        }
        else if (args.Contains("--activeeffecttestonemanarmyqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "one_man_army");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunAutoQa);
        }
        else if (args.Contains("--activeeffecttestpeerlessqa"))
        {
            var index = Enumerable.Range(0, _skillSelect.ItemCount)
                .First(i => _skillSelect.GetItemMetadata(i).AsString() == "peerless");
            _skillSelect.Select(index);
            ResetScenario();
            CallDeferred(MethodName.RunAutoQa);
        }
        else if (args.Contains("--activeeffecttestresetqa"))
        {
            CallDeferred(MethodName.RunResetQa);
        }
        else if (args.Contains("--activeeffecttestpresentqa"))
        {
            CallDeferred(MethodName.RunPresentationQa);
        }
        else if (args.Contains("--activeeffecttesttworoundsqa"))
        {
            CallDeferred(MethodName.RunTwoRoundsQa);
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
        box.AddChild(new Label { Text = $"아군: 주장 제갈량 + 부관 {_adjutant.Name} · 10,000  |  적군: 5부대 × 10,000" });
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
        box.AddChild(new Label { Text = "주장 액티브", Modulate = new Color(1f, 0.82f, 0.42f) });
        box.AddChild(_skillSelect);

        box.AddChild(new Label { Text = "부관 액티브", Modulate = new Color(1f, 0.82f, 0.42f) });
        _adjutantSkillSelect = new OptionButton { CustomMinimumSize = new Vector2(380, 42) };
        _adjutantSkillSelect.AddItem("없음");
        _adjutantSkillSelect.SetItemMetadata(0, "");
        foreach (var skill in _actives.Values.OrderBy(x => x.Type).ThenBy(x => x.Name))
        {
            _adjutantSkillSelect.AddItem($"{skill.Name} · {TypeName(skill.Type)}");
            _adjutantSkillSelect.SetItemMetadata(_adjutantSkillSelect.ItemCount - 1, skill.Code);
        }
        _adjutantSkillSelect.ItemSelected += _ => ResetScenario();
        box.AddChild(_adjutantSkillSelect);

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
        _chargeAppearedDay = 0;
        _allyFiredSkillCodes.Clear();
        _extendedActiveTurns = 0;
        _compressedAdjutantChains = 0;
        _batchAllyActiveFireCount = 0;
        _presentationRunning = false;
        if (_advanceButton is not null) _advanceButton.Disabled = false;
        if (_skillSelect is not null) _skillSelect.Disabled = false;
        if (_adjutantSkillSelect is not null) _adjutantSkillSelect.Disabled = false;

        var selected = SelectedSkill();
        var adjutantSelected = SelectedAdjutantSkill();
        _units =
        [
            MakeUnit(AllyId, 1, new HexCoord(4, 3), 100, selected, adjutantSelected),
            MakeUnit(11, 2, new HexCoord(5, 1), 62),
            MakeUnit(12, 2, new HexCoord(5, 2), 66),
            MakeUnit(13, 2, new HexCoord(5, 3), 70),
            MakeUnit(14, 2, new HexCoord(5, 4), 74),
            MakeUnit(15, 2, new HexCoord(5, 5), 78),
        ];
        foreach (var unit in _units) Spawn(unit);
        FaceOpponents();
        _log.Text = $"[b]전장 초기화[/b]\n주장 제갈량: {selected.Name}\n부관 {_adjutant.Name}: {adjutantSelected?.Name ?? "없음"}\n적군은 이동하지 않으며 전투·피해 계산은 실제 Core 규칙을 사용합니다.\n";
        RefreshSummary(null);
    }

    private CombatUnit MakeUnit(int id, int owner, HexCoord at, int intellect, ActiveSkill? active = null,
        ActiveSkill? adjutantActive = null, int troops = 10000)
    {
        var stats = CombatStatsBuilder.BuildField(
            _template,
            owner == 1 ? AptitudeGrade.A : AptitudeGrade.D,
            0,
            TerrainType.River,
            troops);
        // 이 하베스트의 적군은 액티브를 두 번 관찰할 수 있도록 의도적으로 저전투력으로 둔다.
        if (owner != 1)
            stats = stats with
            {
                AtkStat = Math.Max(1, (int)Math.Round(stats.AtkStat * 0.55)),
                DfStat = Math.Max(1, (int)Math.Round(stats.DfStat * 0.55)),
            };
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), at,
            _template.MovementPerDay, _template.Detection, _template.RangeUnit,
            MovementDomain.Land, UnitMode.Advance, null, id, _template.RangeCastle);
        return new CombatUnit(field, stats, new TroopPool(troops, 0),
            UnitCombatState.Create(intellect, active, adjutantActive), owner == 1 ? _zhugeLiang.Might : 45,
            intellect, troops, _template.Class, TroopCode: _template.Code,
            VanguardId: owner == 1 ? _zhugeLiang.Id : null,
            AdjutantId: owner == 1 ? _adjutant.Id : null);
    }

    private ActiveSkill SelectedSkill()
        => _actives[_skillSelect.GetItemMetadata(_skillSelect.Selected).AsString()];

    private ActiveSkill? SelectedAdjutantSkill()
    {
        var code = _adjutantSkillSelect.GetItemMetadata(_adjutantSkillSelect.Selected).AsString();
        return string.IsNullOrWhiteSpace(code) ? null : _actives[code];
    }

    private void BeginSevenDayPresentation()
    {
        if (_presentationRunning) return;
        _presentationRunning = true;
        foreach (var gauge in _gauges.Values) gauge.Visible = true;
        _advanceButton.Disabled = true;
        _skillSelect.Disabled = true;
        _adjutantSkillSelect.Disabled = true;
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
        _batchAllyActiveFireCount = 0;
        AppendLog($"\n[font_size=20][b]진행 {_advanceCount} · 7일 교전 시작[/b][/font_size]");
    }

    private void RunPresentedDay(int day, int generation)
    {
        if (generation != _presentationGeneration) return;
        var turn = day <= 7 ? AdvanceOneDay(day) : null;
        if (day > 7 || turn is null)
        {
            RefreshSummary(null);
            FinishPresentationEffects();
            _presentationRunning = false;
            _advanceButton.Disabled = false;
            _skillSelect.Disabled = false;
            _adjutantSkillSelect.Disabled = false;
            return;
        }
        RefreshSummary(null);
        var hasActivePresentation = turn.FiredActives.Count > 0;
        if (hasActivePresentation) _extendedActiveTurns++;
        var ally = _units.FirstOrDefault(x => x.Id.Value == AllyId);
        var chainAdjutant = hasActivePresentation
            && turn.FiredActives.ContainsKey(new UnitId(AllyId))
            && _batchAllyActiveFireCount == 1
            && ally?.State.AdjutantActive is not null
            // 첫 진행 뒤 주장=1칸·부관=0칸으로 한 칸 어긋난다. 현재 준비 완료뿐 아니라
            // 바로 다음 전투 일차의 1칸 충전으로 확정 발동하는 경우도 0.2초 연계한다.
            && (ally.State.AdjutantGauge.IsReady || ally.State.AdjutantGauge.Tick(1).IsReady);
        if (chainAdjutant) _compressedAdjutantChains++;
        var delay = chainAdjutant
            ? AdjutantChainDelaySeconds
            : hasActivePresentation ? ActiveDayPresentationSeconds : NormalDayPresentationSeconds;
        var timer = GetTree().CreateTimer(delay);
        timer.Timeout += () => RunPresentedDay(day + 1, generation);
    }

    private AdvanceTurn? AdvanceOneDay(int day)
    {
        if (_units.All(x => x.Field.Owner.Value != 1) || _units.All(x => x.Field.Owner.Value != 2)) return null;
        _round++;
        var beforeUnits = _units.ToDictionary(x => x.Id);
        var before = _units.ToDictionary(x => x.Id, x => x.Pool.Active);
        var turn = _orchestrator.Run(_units, maxDays: 1);
        _units = turn.Units.ToList();

        foreach (var (uid, dealt) in turn.Combat?.DamageDealt ?? new Dictionary<UnitId, int>())
            if (dealt > 0 && _tokens.TryGetValue(uid.Value, out var attacker)) attacker.PlayAttackMotion();
        _lastEffectCount += PlaySkillEffects(turn, beforeUnits);
        RefreshTokens();
        if (_gauges.TryGetValue(AllyId, out var allyGauge)
            && allyGauge.FilledSegments == ActiveGauge.ReadyDays
            && _chargeView is null
            && _tokens.TryGetValue(AllyId, out var caster))
        {
            _chargeView = new ActiveSkillChargeView3D();
            caster.AddChild(_chargeView);
            _chargeAppearedDay = day;
            AppendLog("[color=#ffd05a]액티브 준비 완료 — 발동 대기[/color]");
        }
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
            var casterName = _batchAllyActiveFireCount == 0 ? "제갈량" : _adjutant.Name;
            _allyActiveFireCount++;
            _batchAllyActiveFireCount++;
            _allyActiveFireDay = day;
            _allyFiredSkillCodes.Add(fired.Code);
            AppendLog($"[color=orange][b]{casterName} {fired.Name} 발동[/b][/color]");
            ActiveSkillPresentation.ShowBanner(this, casterName, fired);
            _chargeView?.Complete();
            _chargeView = null;
        }
        return turn;
    }

    private void FinishPresentationEffects()
    {
        foreach (var gauge in _gauges.Values) gauge.Visible = false;
        var charge = _chargeView;
        _chargeView = null;
        if (GodotObject.IsInstanceValid(charge) && !charge!.IsQueuedForDeletion()) charge.QueueFree();
    }

    private void RefreshTokens()
    {
        foreach (var (id, token) in _tokens.ToList())
        {
            var unit = _units.FirstOrDefault(x => x.Id.Value == id);
            if (unit is null)
            {
                // QueueFree는 프레임 끝에 처리된다. 0.2초 뒤 부관 연계가 예약된 경우에도 전멸 부대가
                // 다음 스킬 발동까지 남아 보이지 않도록, 전멸 판정 즉시 렌더링부터 끈다.
                token.Visible = false;
                token.SetProcess(false);
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

    private int PlaySkillEffects(AdvanceTurn turn, IReadOnlyDictionary<UnitId, CombatUnit> beforeUnits)
    {
        if (!turn.FiredActives.TryGetValue(new UnitId(AllyId), out var fired))
        {
            return 0;
        }

        var count = 0;
        if (fired.Type is ActiveType.Defense or ActiveType.Heal && _tokens.TryGetValue(AllyId, out var defensiveCaster))
        {
            var enemy = _units.Where(x => x.Field.Owner.Value == 2)
                .OrderBy(x => x.Field.Position.Distance(beforeUnits[new UnitId(AllyId)].Field.Position))
                .FirstOrDefault();
            var enemyPosition = enemy is not null && _tokens.TryGetValue(enemy.Id.Value, out var enemyToken)
                ? enemyToken.GlobalPosition : defensiveCaster.GlobalPosition + Vector3.Forward;
            if (ActiveSkillPresentation.AttachEffect(defensiveCaster, fired, enemyPosition)) count++;
            if (count > 0) AppendLog($"{fired.Name} 연출: 아군 시전자에 전용 효과 표시");
            return count;
        }
        var targets = fired.Code == "fire_plot"
            ? _units.Where(x => x.Field.Owner.Value == 2 && x.State.Statuses.Any(s => s.IsFire)).ToList()
            : fired.Code == "lightning"
                ? beforeUnits.Values.Where(x => x.Field.Owner.Value == 2
                        && turn.StratagemDamage.GetValueOrDefault(x.Id) > 0)
                    .OrderBy(x => x.Field.Position.Distance(beforeUnits[new UnitId(AllyId)].Field.Position))
                    .Take(1).ToList()
            : fired.Code == "confound"
                ? _units.Where(x => x.Field.Owner.Value == 2 && x.State.Statuses.Any(s => s.IsDaze))
                    .OrderBy(x => x.Field.Position.Distance(beforeUnits[new UnitId(AllyId)].Field.Position))
                    .Take(1).ToList()
            : fired.Code == "rout"
                ? beforeUnits.Values.Where(before => before.Field.Owner.Value == 2
                        && _units.FirstOrDefault(after => after.Id == before.Id)?.Field.Position != before.Field.Position)
                    .OrderBy(before => before.Field.Position.Distance(beforeUnits[new UnitId(AllyId)].Field.Position))
                    .Take(1).ToList()
            : fired.Code is "peerless" or "one_man_army" or "flash" or "barrage" or "reap"
                or "breakthrough" or "tiger_strike" or "chain_strike" or "armor_break" or "heavy_blow" or "double_hit" or "crush"
                ? beforeUnits.Values.Where(x => x.Field.Owner.Value == 2
                    && (turn.Combat?.DamageTaken.GetValueOrDefault(x.Id) ?? 0) > 0)
                    .OrderBy(x => x.Field.Position.Distance(beforeUnits[new UnitId(AllyId)].Field.Position))
                    .Take(1).ToList()
                : [];
        foreach (var target in targets)
        {
            if (!_tokens.TryGetValue(target.Id.Value, out var token)) continue;
            if (fired.Code == "breakthrough" && _tokens.TryGetValue(AllyId, out var breakthroughCaster))
            {
                if (ActiveSkillPresentation.ShowBreakthrough(breakthroughCaster, token)) count++;
                continue;
            }
            if (fired.Code == "tiger_strike" && _tokens.TryGetValue(AllyId, out var tigerCaster))
            {
                if (ActiveSkillPresentation.ShowTigerStrike(tigerCaster, token)) count++;
                continue;
            }
            if (fired.Code is "peerless" or "reap")
            {
                if (ActiveSkillPresentation.AttachEffect(token, fired)) count++;
                continue;
            }
            // 효과를 부대 토큰의 자식으로 붙이면 다음 연계 스킬로 토큰이 전멸할 때 재생 중 효과도
            // 함께 제거된다. 생존 여부와 무관하게 처음부터 마지막 명중 위치의 독립 앵커에 붙인다.
            var deadOnThisHit = _units.All(x => x.Id != target.Id);
            var anchor = new Node3D { Name = deadOnThisHit ? "LethalSkillEffectAnchor" : "ActiveSkillEffectAnchor" };
            anchor.SetMeta("target_unit_id", target.Id.Value);
            anchor.SetMeta("skill_code", fired.Code);
            anchor.SetMeta("advance_count", _advanceCount);
            AddChild(anchor);
            anchor.GlobalPosition = token.GlobalPosition;
            var cleanup = new Godot.Timer { OneShot = true, WaitTime = 2.3 };
            anchor.AddChild(cleanup);
            cleanup.Timeout += anchor.QueueFree;
            cleanup.Start();
            if (ActiveSkillPresentation.AttachEffect(anchor, fired)) count++;
        }
        if (count > 0) AppendLog($"{fired.Name} 연출: 적군 {count}부대에 전용 효과 표시");
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
        var gauge = new ActiveSkillGaugeView3D { Visible = false };
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
        var adjutant = SelectedAdjutantSkill();
        var fired = _allyActiveFireCount > 0 ? $"{_allyActiveFireDay}일차 발동 ({_allyActiveFireCount}회)" : "대기";
        var rows = _units.OrderBy(x => x.Id.Value).Select(x => $"{Tag(x),-8} {x.Pool.Active,6:N0}");
        _summary.Text = $"주장 스킬: {selected.Name} · {TypeName(selected.Type)}\n부관 스킬: {adjutant?.Name ?? "없음"}{(adjutant is null ? "" : $" · {TypeName(adjutant.Type)}")}\n상태: {fired}\n\n병력 현황\n{string.Join("\n", rows)}";
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
        AdvanceSevenDays();
        var ally = _units.FirstOrDefault(x => x.Id.Value == AllyId);
        var expectedSkill = SelectedSkill().Code;
        var fired = ally is not null && _lastEffectCount > 0;
        var swordCountPassed = expectedSkill != "one_man_army"
            || _tokens.Values.SelectMany(x => x.FindChildren("*", "", true, false).OfType<Node>())
                .OfType<OneManArmySwordEffectView3D>().Any(x => x.SwordCount == 3 && x.AnimationClipCount >= 3);
        var flashPassed = expectedSkill != "flash"
            || FindChildren("*", "", true, false).OfType<FlashSlashEffectView3D>()
                .Any(x => x.SlashCount >= 8 && x.HasCrescentBlade && x.SparkCount == 4 && x.FadeDuration >= 0.5f);
        var barragePassed = expectedSkill != "barrage"
            || FindChildren("*", "", true, false).OfType<PeerlessCloudEffectView3D>()
                .Any(x => x.SpawnedBurstCount >= 1);
        var tearPassed = expectedSkill != "peerless"
            || FindChildren("*", "", true, false).OfType<TearEffect>()
                .Any(x => !x.Loop && x.FragmentCount == 4);
        var shatterPassed = expectedSkill != "reap"
            || FindChildren("*", "", true, false).OfType<ShatterEffect>()
                .Any(x => !x.Loop && x.FragmentCount >= 6);
        var breakthroughPassed = expectedSkill != "breakthrough"
            || (_tokens.TryGetValue(AllyId, out var breakthroughCaster)
                && breakthroughCaster.BreakthroughMotionCount == 1
                && breakthroughCaster.LastBreakthroughDistance > 0.48f
                && breakthroughCaster.FindChildren("*", "", true, false)
                    .OfType<BreakthroughSmokeEffectView3D>().Any(x => x.SmokeEmitterCount == 3));
        var gaugePassed = _gauges.TryGetValue(AllyId, out var battleGauge)
            && battleGauge.SkillCode == expectedSkill && battleGauge.FilledSegments == 1
            && battleGauge.HasSpacedHorizontalLayout;
        GD.Print($"[activeeffecttestauto] units={_units.Count} enemy={_units.Count(x => x.Field.Owner.Value == 2)} skill={SelectedSkill().Code} fired={fired} fireDay={_allyActiveFireDay} fireCount={_allyActiveFireCount} effects={_lastEffectCount} days={_round} advances={_advanceCount}");
        var battlePassed = fired && gaugePassed && swordCountPassed && flashPassed && barragePassed
            && tearPassed && shatterPassed && breakthroughPassed && _chargeAppearedDay == 5
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount >= 1
            && _round == 7 && _advanceCount == 1 && ally?.MaxTroops == 10000;
        ResetScenario();
        var resetAlly = _units.FirstOrDefault(x => x.Id.Value == AllyId);
        var resetPassed = _units.Count == 6 && _units.Count(x => x.Field.Owner.Value == 2) == 5
            && _gauges.Count == 6 && resetAlly?.Pool.Active == 10000
            && _gauges[AllyId].SkillCode == expectedSkill && _gauges[AllyId].FilledSegments == 0
            && _round == 0 && _advanceCount == 0 && _allyActiveFireCount == 0
            && _allyActiveFireDay == 0 && _chargeAppearedDay == 0;
        GD.Print($"[activeeffecttestauto] reset={resetPassed} units={_units.Count} ally={resetAlly?.Pool.Active} days={_round}");
        GetTree().Quit(battlePassed && resetPassed ? 0 : 1);
    }

    private void RunBreakthroughQa()
    {
        AdvanceSevenDays();
        var caster = _tokens.GetValueOrDefault(AllyId);
        var started = caster is not null
            && caster.BreakthroughMotionCount == 1
            && caster.LastBreakthroughDistance > 0.48f
            && caster.LastBreakthroughPassPoint.DistanceTo(caster.LastBreakthroughOrigin) > 0.48f
            && caster.FindChildren("*", "", true, false).OfType<BreakthroughSmokeEffectView3D>()
                .Any(x => x.SmokeEmitterCount == 3)
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(0.82);
        timer.Timeout += () =>
        {
            var returned = caster is not null
                && caster.GlobalPosition.DistanceTo(caster.LastBreakthroughOrigin) < 0.01f;
            GD.Print($"[breakthroughqa] started={started} distance={caster?.LastBreakthroughDistance:F2} returned={returned} smoke=3");
            GetTree().Quit(started && returned ? 0 : 1);
        };
    }

    private void RunTigerStrikeQa()
    {
        var allyPosition = _tokens[AllyId].GlobalPosition;
        var expectedDirection = _tokens.Where(x => x.Key != AllyId)
            .OrderBy(x => x.Value.GlobalPosition.DistanceTo(allyPosition))
            .Select(x => x.Value.GlobalPosition - allyPosition)
            .First();
        expectedDirection.Y = 0f;
        expectedDirection = expectedDirection.Normalized();
        AdvanceSevenDays();
        var tiger = FindChildren("*", "", true, false).OfType<TigerStrikeEffectView3D>().FirstOrDefault();
        var spawned = tiger is not null && tiger.TigerCount == 4 && tiger.AttackTravelDistance >= 1f
            && tiger.AttackDirection.Dot(expectedDirection) > 0.99f
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var impactTimer = GetTree().CreateTimer(1.05);
        impactTimer.Timeout += () =>
        {
            var attacked = IsInstanceValid(tiger) && tiger!.ReachedFormationCenterCount == 4
                && tiger.CompletedTigerCount == 4;
            var cleanupTimer = GetTree().CreateTimer(0.38);
            cleanupTimer.Timeout += () =>
            {
                var removed = !IsInstanceValid(tiger) || tiger!.IsQueuedForDeletion();
                GD.Print($"[tigerstrikeqa] spawned={spawned} tigers={tiger?.TigerCount} direction={tiger?.AttackDirection} attacked={attacked} removed={removed} distance={tiger?.AttackTravelDistance:F2}");
                GetTree().Quit(spawned && attacked && removed ? 0 : 1);
            };
        };
    }

    private void RunCrushQa()
    {
        AdvanceSevenDays();
        var shield = FindChildren("*", "", true, false).OfType<CrushShieldBreakEffectView3D>().FirstOrDefault();
        var spawned = shield is not null && shield.FragmentCount == 9 && shield.MaxScatterDistance >= 0.44f
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var impactTimer = GetTree().CreateTimer(1.00);
        impactTimer.Timeout += () =>
        {
            var shattered = IsInstanceValid(shield) && shield!.ShieldAppeared && shield.ShatterCompleted;
            var cleanupTimer = GetTree().CreateTimer(0.30);
            cleanupTimer.Timeout += () =>
            {
                var removed = !IsInstanceValid(shield) || shield!.IsQueuedForDeletion();
                GD.Print($"[crushqa] spawned={spawned} fragments={shield?.FragmentCount} shattered={shattered} removed={removed} scatter={shield?.MaxScatterDistance:F2}");
                GetTree().Quit(spawned && shattered && removed ? 0 : 1);
            };
        };
    }

    private void RunChainStrikeQa()
    {
        AdvanceSevenDays();
        var swords = FindChildren("*", "", true, false).OfType<ChainStrikeSwordEffectView3D>().FirstOrDefault();
        var spawned = swords is not null && swords.SlashCount == 2 && swords.SlashSpan >= 1.3f
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var motionTimer = GetTree().CreateTimer(0.78);
        motionTimer.Timeout += () =>
        {
            var crossed = IsInstanceValid(swords) && swords!.CompletedSlashCount == 2;
            var cleanupTimer = GetTree().CreateTimer(0.42);
            cleanupTimer.Timeout += () =>
            {
                var removed = !IsInstanceValid(swords) || swords!.IsQueuedForDeletion();
                GD.Print($"[chainstrikeqa] spawned={spawned} slashes={swords?.SlashCount} crossed={crossed} removed={removed} span={swords?.SlashSpan:F2}");
                GetTree().Quit(spawned && crossed && removed ? 0 : 1);
            };
        };
    }

    private void RunArmorBreakQa()
    {
        AdvanceSevenDays();
        var armor = FindChildren("*", "", true, false).OfType<ArmorBreakEffectView3D>().FirstOrDefault();
        var spawned = armor is not null && armor.LoadedFromGlb && armor.FragmentCount >= 15 && armor.MaxScatterDistance >= 0.14f
            && armor.ArmorHoldSeconds >= 0.7f
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var breakTimer = GetTree().CreateTimer(2.02);
        breakTimer.Timeout += () =>
        {
            var broken = IsInstanceValid(armor) && armor!.ArmorAppeared && armor.BreakCompleted;
            var cleanupTimer = GetTree().CreateTimer(0.52);
            cleanupTimer.Timeout += () =>
            {
                var removed = !IsInstanceValid(armor) || armor!.IsQueuedForDeletion();
                GD.Print($"[armorbreakqa] spawned={spawned} fragments={armor?.FragmentCount} broken={broken} removed={removed} scatter={armor?.MaxScatterDistance:F2}");
                GetTree().Quit(spawned && broken && removed ? 0 : 1);
            };
        };
    }

    private void RunHeavyBlowQa()
    {
        AdvanceSevenDays();
        var explosion = FindChildren("*", "", true, false).OfType<HeavyBlowExplosionEffectView3D>().FirstOrDefault();
        var spawned = explosion is not null && explosion.LoadedFromGlb && explosion.BombPartCount >= 5
            && explosion.ShardCount == 14 && explosion.SmokeCount == 9
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var explosionTimer = GetTree().CreateTimer(1.18);
        explosionTimer.Timeout += () =>
        {
            var exploded = IsInstanceValid(explosion) && explosion!.ExplosionCompleted;
            var cleanupTimer = GetTree().CreateTimer(0.38);
            cleanupTimer.Timeout += () =>
            {
                var removed = !IsInstanceValid(explosion) || explosion!.IsQueuedForDeletion();
                GD.Print($"[heavyblowqa] spawned={spawned} bomb={explosion?.BombPartCount} shards={explosion?.ShardCount} smoke={explosion?.SmokeCount} exploded={exploded} removed={removed}");
                GetTree().Quit(spawned && exploded && removed ? 0 : 1);
            };
        };
    }

    private void RunDoubleHitQa()
    {
        AdvanceSevenDays();
        var effect = FindChildren("*", "", true, false).OfType<DoubleHitImpactEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb && effect.ImpactCoreCount == 2
            && effect.RingCount == 4 && effect.RayCount == 16
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var impactTimer = GetTree().CreateTimer(0.94);
        impactTimer.Timeout += () =>
        {
            var struckTwice = IsInstanceValid(effect) && effect!.CompletedImpactCount == 2;
            var cleanupTimer = GetTree().CreateTimer(0.42);
            cleanupTimer.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[doublehitqa] spawned={spawned} cores={effect?.ImpactCoreCount} rings={effect?.RingCount} rays={effect?.RayCount} completed={effect?.CompletedImpactCount} removed={removed}");
                GetTree().Quit(spawned && struckTwice && removed ? 0 : 1);
            };
        };
    }

    private void RunIronWallQa()
    {
        AdvanceSevenDays();
        var effect = _tokens[AllyId].FindChildren("*", "", true, false).OfType<IronWallArmorEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb && effect.ChestPlateCount == 5
            && effect.HasProtectionRing && _allyActiveFireDay == 6 && _allyActiveFireCount == 1
            && _lastEffectCount == 1;
        var displayTimer = GetTree().CreateTimer(1.52);
        displayTimer.Timeout += () =>
        {
            var frontFacing = IsInstanceValid(effect) && effect!.TopLevel && effect.IsScreenAligned
                && effect.CameraFacingDot > 0.999f;
            var displayed = IsInstanceValid(effect) && effect!.ArmorAppeared && effect.DisplayCompleted;
            var plateCount = effect?.ChestPlateCount ?? 0;
            var hasRing = effect?.HasProtectionRing ?? false;
            var facingDot = effect?.CameraFacingDot ?? 0f;
            var screenAligned = effect?.IsScreenAligned ?? false;
            var cleanupTimer = GetTree().CreateTimer(0.18);
            cleanupTimer.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[ironwallqa] spawned={spawned} plates={plateCount} ring={hasRing} frontFacing={frontFacing} facingDot={facingDot:F3} screenAligned={screenAligned} displayed={displayed} removed={removed}");
                GetTree().Quit(spawned && frontFacing && displayed && removed ? 0 : 1);
            };
        };
    }

    private void RunRiposteQa()
    {
        AdvanceSevenDays();
        var effect = _tokens[AllyId].FindChildren("*", "", true, false)
            .OfType<RiposteFormationEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb && effect.ShieldLayerCount == 4
            && effect.CounterSpikeCount == 3 && _allyActiveFireDay == 6 && _allyActiveFireCount == 1
            && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(1.12);
        timer.Timeout += () =>
        {
            var displayed = IsInstanceValid(effect) && effect!.DisplayCompleted;
            var frontFacing = IsInstanceValid(effect) && effect!.TopLevel && effect.IsScreenAligned
                && effect.CameraFacingDot > 0.999f;
            var cleanup = GetTree().CreateTimer(0.36);
            cleanup.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[riposteqa] spawned={spawned} displayed={displayed} frontFacing={frontFacing} removed={removed}");
                GetTree().Quit(spawned && displayed && frontFacing && removed ? 0 : 1);
            };
        };
    }

    private void RunTurtleFormationQa()
    {
        AdvanceSevenDays();
        var effect = _tokens[AllyId].FindChildren("*", "", true, false)
            .OfType<TurtleFormationEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb && effect.ShieldCount == 7
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(1.12);
        timer.Timeout += () =>
        {
            var displayed = IsInstanceValid(effect) && effect!.DisplayCompleted && effect.LandedCount == 7;
            var frontFacing = IsInstanceValid(effect) && effect!.TopLevel && effect.IsScreenAligned
                && effect.CameraFacingDot > 0.999f;
            var cleanup = GetTree().CreateTimer(0.42);
            cleanup.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[turtleformationqa] spawned={spawned} landed=7 displayed={displayed} frontFacing={frontFacing} removed={removed}");
                GetTree().Quit(spawned && displayed && frontFacing && removed ? 0 : 1);
            };
        };
    }

    private void RunEvasionQa()
    {
        AdvanceSevenDays();
        var effect = _tokens[AllyId].FindChildren("*", "", true, false)
            .OfType<EvasionWindEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb && effect.WindStreakCount == 5
            && effect.WindMoteCount == 8 && _allyActiveFireDay == 6 && _allyActiveFireCount == 1
            && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(1.32);
        timer.Timeout += () =>
        {
            var displayed = IsInstanceValid(effect) && effect!.SweepCompleted && effect.IsScreenAligned
                && effect.CompletedStreakCount == 5;
            var cleanup = GetTree().CreateTimer(0.32);
            cleanup.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[evasionqa] spawned={spawned} streaks=5 completed=5 motes=8 screenAligned={displayed} removed={removed}");
                GetTree().Quit(spawned && displayed && removed ? 0 : 1);
            };
        };
    }

    private void RunHoldTheLineQa()
    {
        AdvanceSevenDays();
        var effect = _tokens[AllyId].FindChildren("*", "", true, false)
            .OfType<HoldTheLineArrowEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb && effect.ArrowCount == 7
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(1.72);
        timer.Timeout += () =>
        {
            var displayed = IsInstanceValid(effect) && effect!.DisplayCompleted && effect.IsScreenAligned
                && effect.CompletedArrowCount == 7;
            var cleanup = GetTree().CreateTimer(0.32);
            cleanup.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[holdthelineqa] spawned={spawned} arrows=7 completed=7 screenAligned={displayed} removed={removed}");
                GetTree().Quit(spawned && displayed && removed ? 0 : 1);
            };
        };
    }

    private void RunFieldMedicQa()
    {
        AdvanceSevenDays();
        var effect = _tokens[AllyId].FindChildren("*", "", true, false)
            .OfType<FieldMedicCrossEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb && effect.CrossCount == 7
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(1.66);
        timer.Timeout += () =>
        {
            var displayed = IsInstanceValid(effect) && effect!.DisplayCompleted && effect.IsScreenAligned
                && effect.CompletedCrossCount == 7;
            var cleanup = GetTree().CreateTimer(0.32);
            cleanup.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[fieldmedicqa] spawned={spawned} crosses=7 completed=7 screenAligned={displayed} removed={removed}");
                GetTree().Quit(spawned && displayed && removed ? 0 : 1);
            };
        };
    }

    private void RunRegroupQa()
    {
        AdvanceSevenDays();
        var effect = _tokens[AllyId].FindChildren("*", "", true, false)
            .OfType<RegroupSyringeEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(1.18);
        timer.Timeout += () =>
        {
            var animated = IsInstanceValid(effect) && effect!.PlungerPressed && effect.LiquidEmptied
                && effect.IsScreenAligned;
            var cleanup = GetTree().CreateTimer(0.46);
            cleanup.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[regroupqa] spawned={spawned} plungerPressed={animated} liquidEmptied={animated} screenAligned={animated} removed={removed}");
                GetTree().Quit(spawned && animated && removed ? 0 : 1);
            };
        };
    }

    private void RunRallyQa()
    {
        AdvanceSevenDays();
        var effect = _tokens[AllyId].FindChildren("*", "", true, false)
            .OfType<RallyWarDrumEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb && effect.MalletCount == 2 && effect.RingCount == 3
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(1.34);
        timer.Timeout += () =>
        {
            var animated = IsInstanceValid(effect) && effect!.CompletedDrumHits == 4
                && effect.CompletedRingPulses == 3 && effect.IsScreenAligned;
            var cleanup = GetTree().CreateTimer(0.30);
            cleanup.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[rallyqa] spawned={spawned} mallets=2 hits=4 rings=3 pulses=3 screenAligned={animated} removed={removed}");
                GetTree().Quit(spawned && animated && removed ? 0 : 1);
            };
        };
    }

    private void RunResupplyQa()
    {
        AdvanceSevenDays();
        var effect = _tokens[AllyId].FindChildren("*", "", true, false)
            .OfType<ResupplyCrateEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb && effect.BundleCount == 5
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(1.23);
        timer.Timeout += () =>
        {
            var animated = IsInstanceValid(effect) && effect!.LidOpened && effect.DeliveredBundleCount == 5
                && effect.IsScreenAligned;
            var cleanup = GetTree().CreateTimer(0.38);
            cleanup.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[resupplyqa] spawned={spawned} lidOpened={animated} bundles=5 delivered=5 screenAligned={animated} removed={removed}");
                GetTree().Quit(spawned && animated && removed ? 0 : 1);
            };
        };
    }

    private void RunSecondWindQa()
    {
        AdvanceSevenDays();
        var effect = _tokens[AllyId].FindChildren("*", "", true, false)
            .OfType<SecondWindRebirthEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb && effect.SoulCount == 6 && effect.RingCount == 3
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(1.46);
        timer.Timeout += () =>
        {
            var animated = IsInstanceValid(effect) && effect!.ReturnedSoulCount == 6
                && effect.PhoenixRiseCompleted && effect.WingsSpreadCompleted
                && effect.CompletedRingPulses == 3 && effect.IsScreenAligned;
            var cleanup = GetTree().CreateTimer(0.32);
            cleanup.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[secondwindqa] spawned={spawned} embers=6 returned=6 phoenixRise=True wingsSpread=True rings=3 pulses=3 screenAligned={animated} removed={removed}");
                GetTree().Quit(spawned && animated && removed ? 0 : 1);
            };
        };
    }

    private void RunPatchQa()
    {
        AdvanceSevenDays();
        var effect = _tokens[AllyId].FindChildren("*", "", true, false)
            .OfType<PatchCrossEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(1.05);
        timer.Timeout += () =>
        {
            var animated = IsInstanceValid(effect) && effect!.RiseCompleted && effect.IsScreenAligned;
            var cleanup = GetTree().CreateTimer(0.20);
            cleanup.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[patchqa] spawned={spawned} crosses=1 riseCompleted={animated} screenAligned={animated} removed={removed}");
                GetTree().Quit(spawned && animated && removed ? 0 : 1);
            };
        };
    }

    private void RunLightningQa()
    {
        AdvanceSevenDays();
        var effect = FindChildren("*", "", true, false).OfType<LightningGlbEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromGlb && effect.AnimationStarted
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(0.86);
        timer.Timeout += () =>
        {
            var animated = IsInstanceValid(effect) && effect!.AnimationCompleted;
            var cleanup = GetTree().CreateTimer(0.26);
            cleanup.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[lightningqa] spawned={spawned} glb=True animationStarted=True animationCompleted={animated} removed={removed}");
                GetTree().Quit(spawned && animated && removed ? 0 : 1);
            };
        };
    }

    private void RunConfoundQa()
    {
        AdvanceSevenDays();
        var effect = FindChildren("*", "", true, false).OfType<DazeEffect>().FirstOrDefault();
        var targetRecovered = _units.All(x => x.Field.Owner.Value != 2 || x.State.Statuses.All(s => !s.IsDaze));
        var spawned = effect is not null
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(1.42);
        timer.Timeout += () =>
        {
            var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
            GD.Print($"[confoundqa] spawned={spawned} targetAutoRecovered={targetRecovered} dazeEffect={effect is not null} fires={_allyActiveFireCount} fireDay={_allyActiveFireDay} effects={_lastEffectCount} removed={removed}");
            GetTree().Quit(spawned && targetRecovered && removed ? 0 : 1);
        };
    }

    private void RunRoutQa()
    {
        var before = _units.Where(x => x.Field.Owner.Value == 2).ToDictionary(x => x.Id, x => x.Field.Position);
        AdvanceSevenDays();
        var effect = FindChildren("*", "", true, false).OfType<ConfusionEffect>().FirstOrDefault();
        var pushed = _units.Any(x => x.Field.Owner.Value == 2
            && before.TryGetValue(x.Id, out var origin) && x.Field.Position != origin);
        var spawned = effect is not null && pushed
            && _allyActiveFireDay == 6 && _allyActiveFireCount == 1 && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(1.42);
        timer.Timeout += () =>
        {
            var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
            GD.Print($"[routqa] spawned={spawned} targetPushed={pushed} confusionEffect={effect is not null} fires={_allyActiveFireCount} removed={removed}");
            GetTree().Quit(spawned && removed ? 0 : 1);
        };
    }

    private void RunBraceQa()
    {
        AdvanceSevenDays();
        var effect = _tokens[AllyId].FindChildren("*", "", true, false)
            .OfType<BraceArmorEffectView3D>().FirstOrDefault();
        var spawned = effect is not null && effect.LoadedFromIronWallGlb && effect.ChestPlateCount == 5
            && effect.LightenedSurfaceCount > 0 && _allyActiveFireDay == 6 && _allyActiveFireCount == 1
            && _lastEffectCount == 1;
        var timer = GetTree().CreateTimer(0.92);
        timer.Timeout += () =>
        {
            var displayed = IsInstanceValid(effect) && effect!.DisplayCompleted && effect.IsScreenAligned;
            var lightenedCount = effect?.LightenedSurfaceCount ?? 0;
            var cleanup = GetTree().CreateTimer(0.30);
            cleanup.Timeout += () =>
            {
                var removed = !IsInstanceValid(effect) || effect!.IsQueuedForDeletion();
                GD.Print($"[braceqa] spawned={spawned} plates=5 lightened={lightenedCount} screenAligned={displayed} removed={removed}");
                GetTree().Quit(spawned && displayed && removed ? 0 : 1);
            };
        };
    }

    private void RunPresentationQa()
    {
        BeginSevenDayPresentation();
        var timer = GetTree().CreateTimer(10.3);
        timer.Timeout += () =>
        {
            var passed = !_presentationRunning && _round == 7 && _allyActiveFireDay == 6
                && _chargeAppearedDay == 5 && _allyActiveFireCount == 1 && _gauges[AllyId].FilledSegments == 1
                && !_gauges[AllyId].Visible && _chargeView is null
                && _tokens[AllyId].FindChild("Effect_Burst", true, false) is null
                && _extendedActiveTurns == 1 && ActiveDayPresentationSeconds >= 1.55
                // 액티브 발동일은 일반 공격을 대체한다: 일반 공격 6회 + 액티브 1회 = 7일의 가시 행동.
                && _tokens[AllyId].AttackMotionStartCount + _allyActiveFireCount == 7;
            GD.Print($"[activeeffecttestpresentqa] passed={passed} days={_round} attacks={_tokens[AllyId].AttackMotionStartCount} actives={_allyActiveFireCount} fireDay={_allyActiveFireDay} activeTurns={_extendedActiveTurns} activeSeconds={ActiveDayPresentationSeconds:F2} gauge={_gauges[AllyId].FilledSegments} visible={_gauges[AllyId].Visible} burstAlive={_tokens[AllyId].FindChild("Effect_Burst", true, false) is not null}");
            GetTree().Quit(passed ? 0 : 1);
        };
    }

    private void RunAdjutantQa()
    {
        var expectedVanguard = SelectedSkill().Code;
        var expectedAdjutant = SelectedAdjutantSkill()?.Code;
        AdvanceSevenDays();
        var ally = _units.Single(x => x.Id.Value == AllyId);
        var passed = expectedAdjutant is not null
            && ally.AdjutantId == _adjutant.Id
            && ally.State.VanguardActive?.Code == expectedVanguard
            && ally.State.AdjutantActive?.Code == expectedAdjutant
            && _allyFiredSkillCodes.SequenceEqual(new[] { expectedVanguard, expectedAdjutant })
            && _allyActiveFireCount == 2 && _round == 7;
        GD.Print($"[activeeffecttestadjutantqa] passed={passed} adjutant={_adjutant.Name} vanguard={expectedVanguard} adjutantSkill={expectedAdjutant} fired={string.Join(",", _allyFiredSkillCodes)}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void RunAdjutantPresentationQa()
    {
        var expectedVanguard = SelectedSkill().Code;
        var expectedAdjutant = SelectedAdjutantSkill()!.Code;
        BeginSevenDayPresentation();
        var timer = GetTree().CreateTimer(9.2);
        timer.Timeout += () =>
        {
            var passed = !_presentationRunning && _round == 7 && _extendedActiveTurns == 2
                && _allyFiredSkillCodes.SequenceEqual(new[] { expectedVanguard, expectedAdjutant })
                && _allyActiveFireCount == 2 && _chargeView is null
                && _compressedAdjutantChains == 1 && AdjutantChainDelaySeconds == 0.20
                && ActiveDayPresentationSeconds >= 1.85;
            GD.Print($"[activeeffecttestadjutantpresentqa] passed={passed} fired={string.Join(",", _allyFiredSkillCodes)} activeTurns={_extendedActiveTurns} chains={_compressedAdjutantChains} chainDelay={AdjutantChainDelaySeconds:F2}");
            GetTree().Quit(passed ? 0 : 1);
        };
    }

    private void RunLethalAdjutantEffectQa()
    {
        BeginAdvanceBatch();
        for (var day = 1; day <= 6; day++)
        {
            if (AdvanceOneDay(day) is null) break;
        }

        var enemiesBefore = _units.Where(x => x.Field.Owner.Value == 2).Select(x => x.Id).ToHashSet();
        _units = _units.Select(x => x.Field.Owner.Value == 2
            ? x with { Pool = x.Pool with { Active = 1 } }
            : x).ToList();
        var turn = AdvanceOneDay(7);
        var enemiesAfter = _units.Where(x => x.Field.Owner.Value == 2).Select(x => x.Id).ToHashSet();
        var anchor = FindChild("LethalSkillEffectAnchor", true, false);
        var passed = turn is not null
            && turn.FiredActives.TryGetValue(new UnitId(AllyId), out var fired)
            && fired.Code == "one_man_army"
            && enemiesBefore.Except(enemiesAfter).Any()
            && anchor is not null
            && anchor.FindChildren("*", "", true, false).OfType<OneManArmySwordEffectView3D>().Any()
            && _lastEffectCount > 0;
        GD.Print($"[activeeffecttestlethaladjutantqa] passed={passed} before={enemiesBefore.Count} after={enemiesAfter.Count} effectCount={_lastEffectCount} anchor={anchor is not null}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void RunSecondAdvanceLethalQa()
    {
        // 1회차 종료 시 두 게이지가 1칸 남는 실제 재현 조건.
        AdvanceSevenDays();
        BeginAdvanceBatch();
        for (var day = 1; day <= 4; day++)
        {
            if (AdvanceOneDay(day) is null) break;
        }

        var enemyIdsBefore = _units.Where(x => x.Field.Owner.Value == 2).Select(x => x.Id).ToHashSet();
        var tokenRefs = enemyIdsBefore.ToDictionary(id => id, id => _tokens[id.Value]);
        var allyPosition = _units.Single(x => x.Id.Value == AllyId).Field.Position;
        var lethalTarget = _units.Where(x => x.Field.Owner.Value == 2)
            .OrderBy(x => x.Field.Position.Distance(allyPosition)).ThenBy(x => x.Id.Value).First().Id;
        var followUpTarget = _units.Where(x => x.Field.Owner.Value == 2 && x.Id != lethalTarget)
            .OrderBy(x => x.Field.Position.Distance(allyPosition)).ThenBy(x => x.Id.Value).First().Id;
        _units = _units.Select(x => x.Id == lethalTarget
            ? x with { Pool = x.Pool with { Active = 1 } }
            : x.Id == followUpTarget
                ? x with { Field = x.Field with { Position = new HexCoord(4, 4) } }
            : x).ToList();

        var vanguardTurn = AdvanceOneDay(5);
        var enemyIdsAfterVanguard = _units.Where(x => x.Field.Owner.Value == 2).Select(x => x.Id).ToHashSet();
        var removed = enemyIdsBefore.Except(enemyIdsAfterVanguard).ToList();
        var removedImmediatelyHidden = removed.Count > 0 && removed.All(id =>
            !tokenRefs[id].Visible && tokenRefs[id].IsQueuedForDeletion());

        var adjutantTurn = AdvanceOneDay(6);
        var passed = vanguardTurn is not null && adjutantTurn is not null
            && vanguardTurn.FiredActives.TryGetValue(new UnitId(AllyId), out var vanguard)
            && vanguard.Code == "one_man_army"
            && adjutantTurn.FiredActives.TryGetValue(new UnitId(AllyId), out var adjutant)
            && adjutant.Code == "peerless"
            && removedImmediatelyHidden
            && _allyFiredSkillCodes.TakeLast(2).SequenceEqual(new[] { "one_man_army", "peerless" });
        GD.Print($"[activeeffecttestsecondadvancelethalqa] passed={passed} removed={removed.Count} hiddenBeforeAdjutant={removedImmediatelyHidden} fired={string.Join(",", _allyFiredSkillCodes.TakeLast(2))}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void RunTwoPresentationQa()
    {
        BeginSevenDayPresentation();
        var first = GetTree().CreateTimer(9.1);
        first.Timeout += () =>
        {
            var firstPassed = !_presentationRunning && _advanceCount == 1
                && _compressedAdjutantChains == 1 && _batchAllyActiveFireCount == 2
                && _allyFiredSkillCodes.TakeLast(2).SequenceEqual(new[] { "one_man_army", "peerless" });
            if (!firstPassed)
            {
                GD.Print($"[activeeffecttesttwopresentationqa] passed=False stage=first chains={_compressedAdjutantChains} batchFires={_batchAllyActiveFireCount}");
                GetTree().Quit(1);
                return;
            }

            BeginSevenDayPresentation();
            var second = GetTree().CreateTimer(9.1);
            second.Timeout += () =>
            {
                var expected = new[] { "one_man_army", "peerless", "one_man_army", "peerless" };
                var passed = !_presentationRunning && _advanceCount == 2
                    && _compressedAdjutantChains == 2 && _batchAllyActiveFireCount == 2
                    && _allyFiredSkillCodes.TakeLast(4).SequenceEqual(expected);
                GD.Print($"[activeeffecttesttwopresentationqa] passed={passed} advances={_advanceCount} chains={_compressedAdjutantChains} batchFires={_batchAllyActiveFireCount} fired={string.Join(",", _allyFiredSkillCodes.TakeLast(4))}");
                GetTree().Quit(passed ? 0 : 1);
            };
        };
    }

    private void RunEffectSurvivesDeathQa()
    {
        AdvanceSevenDays();
        var anchor = FindChildren("*", "", true, false)
            .OfType<Node3D>()
            .LastOrDefault(x => x.HasMeta("skill_code") && x.GetMeta("skill_code").AsString() == "one_man_army");
        var targetId = anchor?.GetMeta("target_unit_id").AsInt32() ?? -1;
        var tokenRemovedForQa = false;
        if (targetId > 0 && _tokens.TryGetValue(targetId, out var token))
        {
            token.Visible = false;
            token.QueueFree();
            tokenRemovedForQa = true;
        }

        var swordEffect = anchor?.FindChildren("*", "", true, false)
            .OfType<OneManArmySwordEffectView3D>().FirstOrDefault();
        var effectStillAlive = GodotObject.IsInstanceValid(anchor) && !anchor!.IsQueuedForDeletion()
            && GodotObject.IsInstanceValid(swordEffect) && !swordEffect!.IsQueuedForDeletion();
        var independentFromToken = anchor?.GetParent() == this && swordEffect?.GetParent() == anchor;
        var passed = targetId > 0 && tokenRemovedForQa && effectStillAlive && independentFromToken;
        GD.Print($"[activeeffecttesteffectsurvivesdeathqa] passed={passed} target={targetId} tokenRemoved={tokenRemovedForQa} firstEffectAlive={effectStillAlive} independent={independentFromToken}");
        GetTree().Quit(passed ? 0 : 1);
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
                    && _chargeView is null && ally?.Pool.Active == 10000 && !_advanceButton.Disabled && !_skillSelect.Disabled
                    && !_adjutantSkillSelect.Disabled && !_gauges[AllyId].Visible;
                GD.Print($"[activeeffecttestresetqa] passed={passed} days={_round} advances={_advanceCount} ally={ally?.Pool.Active}");
                GetTree().Quit(passed ? 0 : 1);
            };
        };
    }

    private void RunTwoRoundsQa()
    {
        var allyBefore = _units.Single(x => x.Id.Value == AllyId);
        var enemyBefore = _units.First(x => x.Field.Owner.Value == 2);
        var enemyWeaker = enemyBefore.Stats.AtkStat < allyBefore.Stats.AtkStat
            && enemyBefore.Stats.DfStat < allyBefore.Stats.DfStat
            && enemyBefore.Stats.AptitudePercent < allyBefore.Stats.AptitudePercent;

        AdvanceSevenDays();
        AdvanceSevenDays();

        var ally = _units.FirstOrDefault(x => x.Id.Value == AllyId);
        var enemies = _units.Count(x => x.Field.Owner.Value == 2);
        var passed = enemyWeaker && _advanceCount == 2 && _round == 14
            && _allyActiveFireCount == 2 && ally is not null && ally.Pool.Active > 0 && enemies > 0;
        GD.Print($"[activeeffecttesttworoundsqa] passed={passed} weaker={enemyWeaker} days={_round} fires={_allyActiveFireCount} ally={ally?.Pool.Active} enemies={enemies}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
