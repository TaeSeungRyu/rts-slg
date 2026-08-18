using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

/// <summary>
/// 내정 전용 게임 씬(12b) — 삼국지11식 계단식 명령 팔레트. 성 클릭 → 명령 목록 → 파라미터·장수 목록
/// → 장수 클릭 → 컨펌창 → 실행(모든 명령·진행 컨펌, design-ui #4). 장수 목록에 고향 배지·전제 미충족
/// 표시(design-ui #5), 컨펌창에 유효 능력. Core <see cref="AdminSession"/>을 호출·반영만 한다
/// (노드에 규칙 없음 — CLAUDE.md). 전투 없음.
/// </summary>
public sealed partial class AdminScene : Control
{
    private AdminSession _session = null!;
    private CommandBalance _commandBalance = null!;
    private IReadOnlyList<TroopTemplate> _troops = null!;

    private Label _date = null!;
    private Label _result = null!;
    private VBoxContainer _cityCol = null!;
    private PanelContainer _cmdPanel = null!;
    private VBoxContainer _cmdCol = null!;
    private PanelContainer _detailPanel = null!;
    private VBoxContainer _detailCol = null!;
    private ConfirmationDialog _confirm = null!;

    private CityId? _city;
    private OptionButton? _troopSel;
    private OptionButton? _facilitySel;
    private OptionButton? _stratagemSel;
    private OptionButton? _targetSel;
    private readonly List<CityId> _targetIds = new();
    private SpinBox? _taxSpin;
    private Action? _onConfirm;

    private static readonly (string Label, CommandKind Kind, string Param)[] Commands =
    {
        ("모병", CommandKind.Recruit, "troop"),
        ("징병", CommandKind.Conscript, "troop"),
        ("훈련", CommandKind.Train, "troop"),
        ("건설", CommandKind.Build, "facility"),
        ("세율", CommandKind.SetTaxRate, "tax"),
        ("병종 연구", CommandKind.Research, "troop"),
        ("성벽 연구", CommandKind.Research, "wall"),
        ("성벽 수리", CommandKind.Repair, "wall"),
        ("시설 수리", CommandKind.Repair, "repairable"),
        ("도시 계략", CommandKind.CityStratagem, "stratagem"),
    };

    private static readonly (string Label, string Code)[] Stratagems =
    {
        ("정찰", "scout"), ("성벽파괴", "wall_break"), ("선동", "incite"),
        ("방화", "arson"), ("절취", "steal"), ("이간", "sow_discord"),
    };

    private static readonly (string Label, string Code)[] Facilities =
    {
        ("논", "paddy"), ("밭", "farm"), ("마을", "village"), ("공방", "workshop"),
    };

    private static readonly (string Label, string Code)[] Repairables =
    {
        ("논", "paddy"), ("밭", "farm"), ("마을", "village"), ("공방", "workshop"),
        ("광산", "mine"), ("목장", "ranch"), ("상원", "elephant_garden"),
    };

    public void Build(string dataDirectory)
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(dataDirectory);
        _commandBalance = new CommandBalanceLoader().LoadFromDirectory(dataDirectory);
        _troops = new TroopTypeLoader().LoadFromDirectory(dataDirectory);
        var adminSkills = new AdminSkillLoader().LoadFromDirectory(dataDirectory);

        var player = scenario.Factions.OrderBy(f => f.Id.Value).First().Id;
        _session = new AdminSession(
            GameState.FromScenario(scenario), player,
            new CommandService(_commandBalance, _troops, scenario.Balance),
            new WorldEngine(scenario.Balance, _commandBalance, adminSkills));

        BuildUi();
        GetWindow().MinSize = new Vector2I(1040, 660); // 작은 창이면 컬럼이 눌리므로 최소 크기 보장
        RebuildCities();
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var root = new MarginContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            root.AddThemeConstantOverride(side, 22);
        }

        AddChild(root);

        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 12);
        root.AddChild(outer);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 16);
        outer.AddChild(header);
        _date = new Label();
        _date.AddThemeFontSizeOverride("font_size", 22);
        header.AddChild(_date);
        var advance = new Button { Text = "진행 (7일)", CustomMinimumSize = new Vector2(140, 38) };
        advance.Pressed += OnAdvance;
        header.AddChild(advance);

        _result = new Label
        {
            Text = "성을 클릭해 명령을 내리세요.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _result.AddThemeFontSizeOverride("font_size", 15);
        outer.AddChild(_result);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 12);
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        outer.AddChild(columns);

        ColumnBody(columns, "도시", 260, out _cityCol);
        _cmdPanel = ColumnBody(columns, "명령", 150, out _cmdCol);
        _detailPanel = ColumnBody(columns, "수행", 260, out _detailCol);
        _cmdPanel.Visible = false;
        _detailPanel.Visible = false;

        _confirm = new ConfirmationDialog { Title = "확인" };
        _confirm.Confirmed += () => _onConfirm?.Invoke();
        AddChild(_confirm);
    }

    private static PanelContainer ColumnBody(Container parent, string title, int width, out VBoxContainer body)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        parent.AddChild(panel);
        var inner = new VBoxContainer();
        inner.AddThemeConstantOverride("separation", 6);
        panel.AddChild(inner);
        var label = new Label { Text = title };
        label.AddThemeFontSizeOverride("font_size", 16);
        inner.AddChild(label);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        inner.AddChild(scroll);
        body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(body);
        return panel;
    }

    // 진행도 컨펌 후(design-ui #4).
    private void OnAdvance()
    {
        Ask("7일 진행하시겠습니까?", () =>
        {
            _session.AdvanceWeek();
            _cmdPanel.Visible = false;
            _detailPanel.Visible = false;
            _city = null;
            _result.Text = "진행했습니다. 성을 클릭해 명령을 내리세요.";
            RebuildCities();
        });
    }

    // ── 컬럼 1: 도시(성) ──
    private void RebuildCities()
    {
        var s = _session.State;
        _date.Text = $"{s.Factions.First(f => f.Id == _session.Player).Name}   {s.Year}년 {s.Month}월 {s.DayOfMonth}일";
        Clear(_cityCol);

        foreach (var city in _session.PlayerCities())
        {
            var troops = s.Garrisons.Where(g => g.City == city.Id).Sum(g => g.Troops);
            var pending = _session.PendingAt(city.Id).Count;
            var mark = _city == city.Id ? "▶ " : "";
            var pendingText = pending > 0 ? $" 진행중{pending}" : "";
            var btn = new Button
            {
                Text = $"{mark}[{Size(city.Castle)}] {city.Name}  금{city.Gold} 성벽{city.Wall} 병{troops}{pendingText}",
                CustomMinimumSize = new Vector2(0, 38),
                Alignment = HorizontalAlignment.Left,
            };
            var id = city.Id;
            btn.Pressed += () => OnCityClicked(id);
            _cityCol.AddChild(btn);
        }
    }

    private void OnCityClicked(CityId city)
    {
        _city = city;
        var c = _session.State.Cities.First(x => x.Id == city);

        var pendingLines = _session.PendingAt(city).Select(p =>
            $"{Kind(p.Kind)}{(p.TroopCode.Length > 0 && p.TroopCode != FactionResearch.WallCode ? " " + p.TroopCode : "")} (남은 {p.CompletionDay - _session.State.Day}일)");
        var pendingText = pendingLines.Any() ? "  |  진행중: " + string.Join(", ", pendingLines) : "";
        _result.Text = $"{c.Name} — 금 {c.Gold} 군량 {c.Provisions} 인구 {c.Population} 치안 {c.Security} " +
            $"세율 {c.TaxRate}% 성벽 {c.Wall} | 광석 {c.Ore} 말 {c.Horses} 코끼리 {c.Elephants} | " +
            $"논{c.Paddies} 밭{c.Farms} 마을{c.Villages}{(c.Workshop ? " 공방" : "")}{pendingText}";

        Clear(_cmdCol);
        for (var i = 0; i < Commands.Length; i++)
        {
            var idx = i;
            var btn = new Button { Text = Commands[i].Label, CustomMinimumSize = new Vector2(0, 34) };
            btn.Pressed += () => OnCommandClicked(idx);
            _cmdCol.AddChild(btn);
        }

        _cmdPanel.Visible = true;
        _detailPanel.Visible = false;
        RebuildCities(); // 선택 성 ▶ 표식
    }

    // ── 컬럼 2: 명령 → 컬럼 3(파라미터 + 장수 목록) ──
    private void OnCommandClicked(int cmdIndex)
    {
        if (_city is not { } city)
        {
            return;
        }

        var cmd = Commands[cmdIndex];
        var cityData = _session.State.Cities.First(x => x.Id == city);
        Clear(_detailCol);
        _troopSel = null;
        _facilitySel = null;
        _stratagemSel = null;
        _targetSel = null;
        _targetIds.Clear();
        _taxSpin = null;

        if (cmd.Param == "troop")
        {
            _detailCol.AddChild(new Label { Text = "병종" });
            _troopSel = new OptionButton();
            foreach (var t in _troops)
            {
                _troopSel.AddItem(t.Name);
            }

            _detailCol.AddChild(_troopSel);
        }
        else if (cmd.Param is "facility" or "repairable")
        {
            _detailCol.AddChild(new Label { Text = "시설" });
            _facilitySel = new OptionButton();
            foreach (var f in cmd.Param == "facility" ? Facilities : Repairables)
            {
                _facilitySel.AddItem(f.Label);
            }

            _detailCol.AddChild(_facilitySel);
        }
        else if (cmd.Param == "tax")
        {
            _detailCol.AddChild(new Label { Text = "세율(%)" });
            _taxSpin = new SpinBox { MinValue = 0, MaxValue = 50, Step = 5, Value = cityData.TaxRate };
            _detailCol.AddChild(_taxSpin);
        }
        else if (cmd.Param == "stratagem")
        {
            _detailCol.AddChild(new Label { Text = "계략" });
            _stratagemSel = new OptionButton();
            foreach (var st in Stratagems)
            {
                _stratagemSel.AddItem(st.Label);
            }

            _detailCol.AddChild(_stratagemSel);

            _detailCol.AddChild(new Label { Text = "대상(적 도시)" });
            _targetSel = new OptionButton();
            foreach (var enemy in _session.State.Cities.Where(c => c.Owner != _session.Player).OrderBy(c => c.Id.Value))
            {
                var scouted = _session.State.IsScouted(_session.Player, enemy.Id) ? " (정찰됨)" : "";
                _targetSel.AddItem($"{enemy.Name}{scouted}");
                _targetIds.Add(enemy.Id);
            }

            _detailCol.AddChild(_targetSel);
        }

        _detailCol.AddChild(new Label { Text = "수행 장수" });
        var generals = _session.AvailableGenerals(city).ToList();
        if (generals.Count == 0)
        {
            _detailCol.AddChild(new Label { Text = "(가능한 장수 없음)" });
        }

        foreach (var gid in generals)
        {
            var g = _session.State.Generals.First(x => x.Id == gid);
            var home = g.Region.Length > 0 && g.Region == cityData.Region ? $" 🏠+{_commandBalance.HomeRegionBonusPercent}%" : "";
            var blocked = cmd.Kind == CommandKind.Build && g.Politics <= _commandBalance.BuildPoliticsRequired;
            var btn = new Button
            {
                Text = blocked ? $"{g.Name} (정치 부족)" : $"{g.Name}{home}",
                Disabled = blocked,
                CustomMinimumSize = new Vector2(0, 32),
            };
            var captured = gid;
            btn.Pressed += () => OnGeneralClicked(city, cmdIndex, captured);
            _detailCol.AddChild(btn);
        }

        _detailPanel.Visible = true;
    }

    // ── 컬럼 3: 장수 클릭 → 컨펌창 ──
    private void OnGeneralClicked(CityId city, int cmdIndex, GeneralId general)
    {
        var cmd = Commands[cmdIndex];
        var troopCode = cmd.Param switch
        {
            "troop" => _troops[Math.Max(0, _troopSel?.Selected ?? 0)].Code,
            "wall" => FactionResearch.WallCode,
            _ => "",
        };
        var facility = cmd.Param switch
        {
            "facility" => Facilities[Math.Max(0, _facilitySel?.Selected ?? 0)].Code,
            "repairable" => Repairables[Math.Max(0, _facilitySel?.Selected ?? 0)].Code,
            "stratagem" => Stratagems[Math.Max(0, _stratagemSel?.Selected ?? 0)].Code,
            _ => "",
        };
        var value = cmd.Param == "tax" ? (int)(_taxSpin?.Value ?? 0) : 0;
        CityId? targetCity = cmd.Param == "stratagem" && _targetIds.Count > 0
            ? _targetIds[Math.Max(0, _targetSel?.Selected ?? 0)]
            : null;
        var request = new CommandRequest(city, cmd.Kind, general, Value: value, Facility: facility,
            TroopCode: troopCode, TargetCity: targetCity);

        var cityData = _session.State.Cities.First(x => x.Id == city);
        var g = _session.State.Generals.First(x => x.Id == general);
        var paramText = cmd.Param switch
        {
            "troop" => $" · {_troops[Math.Max(0, _troopSel?.Selected ?? 0)].Name}",
            "facility" => $" · {Facilities[Math.Max(0, _facilitySel?.Selected ?? 0)].Label}",
            "repairable" => $" · {Repairables[Math.Max(0, _facilitySel?.Selected ?? 0)].Label}",
            "stratagem" => $" · {Stratagems[Math.Max(0, _stratagemSel?.Selected ?? 0)].Label}",
            "tax" => $" · {value}%",
            _ => "",
        };
        var label = $"{cmd.Label}{paramText}";

        // 도시 계략은 소요일(거리 비례)·성공률을 발행 전에 확정 표시한다(모든 계략 사전 컨펌).
        var extra = "";
        if (cmd.Param == "stratagem" && targetCity is { } tid)
        {
            var target = _session.State.Cities.First(x => x.Id == tid);
            var defenderInt = target.Governor is { } govId
                ? _session.State.Generals.FirstOrDefault(x => x.Id == govId)?.Intellect
                : null;
            var days = CityStratagems.Days(cityData.Position, target.Position, _commandBalance);
            var success = CityStratagems.SuccessPercent(g.Intellect, defenderInt);
            extra = $"\n대상: {target.Name} · 소요 {days}일 · 성공률 {success}%";
        }

        _confirm.DialogText = $"{cityData.Name} — {label}{extra}\n수행 장수: {g.Name}   ({EffText(cmd.Kind, g, cityData)})\n\n실행하시겠습니까?";
        Ask(_confirm.DialogText, () =>
        {
            var result = _session.Issue(request);
            _result.Text = result.Ok ? $"발행: {label} — {g.Name}" : $"실패: {result.Error}";
            _cmdPanel.Visible = false;
            _detailPanel.Visible = false;
            _city = null;
            RebuildCities();
        });
    }

    // 컨펌창을 띄우고 확인 시 action 실행.
    private void Ask(string text, Action action)
    {
        _onConfirm = action;
        _confirm.DialogText = text;
        _confirm.PopupCentered();
    }

    // 수행 장수의 유효 능력·고향 표시(Core CommandEfficiency).
    private string EffText(CommandKind kind, General g, City city)
    {
        var home = g.Region.Length > 0 && g.Region == city.Region ? $" 🏠+{_commandBalance.HomeRegionBonusPercent}%" : "";
        return kind switch
        {
            CommandKind.Recruit or CommandKind.Conscript or CommandKind.Build =>
                $"유효 정치 {CommandEfficiency.Effective(g, null, city, kind, _commandBalance)}{home}",
            CommandKind.Train => $"유효 무력 {CommandEfficiency.Effective(g, null, city, kind, _commandBalance)}{home}",
            CommandKind.Research or CommandKind.CityStratagem => $"지력 {g.Intellect}",
            _ => "효율 무관",
        };
    }

    private static void Clear(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static string Kind(CommandKind k) => k switch
    {
        CommandKind.Recruit => "모병",
        CommandKind.Conscript => "징병",
        CommandKind.Train => "훈련",
        CommandKind.Build => "건설",
        CommandKind.SetTaxRate => "세율",
        CommandKind.Research => "연구",
        _ => k.ToString(),
    };

    private static string Size(CastleSize castle) => castle switch
    {
        CastleSize.Large => "대",
        CastleSize.Medium => "중",
        _ => "소",
    };
}
