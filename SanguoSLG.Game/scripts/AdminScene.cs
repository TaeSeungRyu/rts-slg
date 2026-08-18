using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

/// <summary>
/// 내정 전용 게임 씬(12b) — 삼국지11식 계단식 명령 팔레트. 성을 클릭해 선택하면 명령 목록이 옆에
/// 붙고, 명령을 고르면 파라미터·장수 목록이 다시 옆에 붙는다(계단식). 장수를 고르면 컨펌창을 거쳐
/// 실행한다(모든 명령 컨펌). Core <see cref="AdminSession"/>을 호출·반영만 한다(노드에 규칙 없음 —
/// CLAUDE.md). 전투 없음.
/// </summary>
public sealed partial class AdminScene : Control
{
    private AdminSession _session = null!;
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
    private SpinBox? _taxSpin;
    private CommandRequest? _pending;
    private string _pendingLabel = "";

    private static readonly (string Label, CommandKind Kind, string Param)[] Commands =
    {
        ("모병", CommandKind.Recruit, "troop"),
        ("징병", CommandKind.Conscript, "troop"),
        ("훈련", CommandKind.Train, "troop"),
        ("건설", CommandKind.Build, "facility"),
        ("세율", CommandKind.SetTaxRate, "tax"),
        ("병종 연구", CommandKind.Research, "troop"),
        ("성벽 연구", CommandKind.Research, "wall"),
    };

    private static readonly (string Label, string Code)[] Facilities =
    {
        ("논", "paddy"), ("밭", "farm"), ("마을", "village"), ("공방", "workshop"),
    };

    public void Build(string dataDirectory)
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(dataDirectory);
        var commandBalance = new CommandBalanceLoader().LoadFromDirectory(dataDirectory);
        _troops = new TroopTypeLoader().LoadFromDirectory(dataDirectory);
        var adminSkills = new AdminSkillLoader().LoadFromDirectory(dataDirectory);

        var player = scenario.Factions.OrderBy(f => f.Id.Value).First().Id;
        _session = new AdminSession(
            GameState.FromScenario(scenario), player,
            new CommandService(commandBalance, _troops),
            new WorldEngine(scenario.Balance, commandBalance, adminSkills));

        BuildUi();
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

        _result = new Label { Text = "성을 클릭해 명령을 내리세요." };
        _result.AddThemeFontSizeOverride("font_size", 15);
        outer.AddChild(_result);

        // 계단식 컬럼: [도시] [명령] [세부(파라미터·장수)]
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 12);
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        outer.AddChild(columns);

        ColumnBody(columns, "도시", 240, out _cityCol);
        _cmdPanel = ColumnBody(columns, "명령", 150, out _cmdCol);
        _detailPanel = ColumnBody(columns, "수행", 230, out _detailCol);
        _cmdPanel.Visible = false;
        _detailPanel.Visible = false;

        _confirm = new ConfirmationDialog { Title = "명령 확인" };
        _confirm.Confirmed += OnConfirmed;
        AddChild(_confirm);
    }

    // 제목 라벨 + 스크롤 내용 VBox를 담은 패널 컬럼을 만든다. 패널을 반환하고 내용 VBox를 out으로.
    private static PanelContainer ColumnBody(Container parent, string title, int width, out VBoxContainer body)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill, // HBox가 세로로 늘려줘야 스크롤·버튼이 펼쳐진다
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

    private void OnAdvance()
    {
        _session.AdvanceWeek();
        _cmdPanel.Visible = false;
        _detailPanel.Visible = false;
        _city = null;
        _result.Text = "진행했습니다. 성을 클릭해 명령을 내리세요.";
        RebuildCities();
    }

    // ── 컬럼 1: 도시(성) 클릭 ──
    private void RebuildCities()
    {
        var s = _session.State;
        _date.Text = $"{s.Factions.First(f => f.Id == _session.Player).Name}   {s.Year}년 {s.Month}월 {s.DayOfMonth}일";
        Clear(_cityCol);

        foreach (var city in _session.PlayerCities())
        {
            var troops = s.Garrisons.Where(g => g.City == city.Id).Sum(g => g.Troops);
            var pending = _session.PendingAt(city.Id).Count;
            var pendingText = pending > 0 ? $"  진행중 {pending}" : "";
            var btn = new Button
            {
                Text = $"[{Size(city.Castle)}] {city.Name}  금{city.Gold} 성벽{city.Wall} 병{troops}{pendingText}",
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
        _result.Text = $"{c.Name} — 금 {c.Gold} 군량 {c.Provisions} 인구 {c.Population} 치안 {c.Security} " +
            $"세율 {c.TaxRate}% 성벽 {c.Wall} | 광석 {c.Ore} 말 {c.Horses} 코끼리 {c.Elephants} | " +
            $"논{c.Paddies} 밭{c.Farms} 마을{c.Villages}{(c.Workshop ? " 공방" : "")}";

        // 컬럼 2: 명령 목록을 옆에 붙인다.
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
    }

    // ── 컬럼 2: 명령 클릭 → 컬럼 3(파라미터 + 장수 목록) ──
    private void OnCommandClicked(int cmdIndex)
    {
        if (_city is not { } city)
        {
            return;
        }

        var cmd = Commands[cmdIndex];
        Clear(_detailCol);
        _troopSel = null;
        _facilitySel = null;
        _taxSpin = null;

        // 파라미터 컨트롤(명령별).
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
        else if (cmd.Param == "facility")
        {
            _detailCol.AddChild(new Label { Text = "시설" });
            _facilitySel = new OptionButton();
            foreach (var f in Facilities)
            {
                _facilitySel.AddItem(f.Label);
            }

            _detailCol.AddChild(_facilitySel);
        }
        else if (cmd.Param == "tax")
        {
            _detailCol.AddChild(new Label { Text = "세율(%)" });
            _taxSpin = new SpinBox { MinValue = 0, MaxValue = 50, Step = 5, Value = _session.State.Cities.First(x => x.Id == city).TaxRate };
            _detailCol.AddChild(_taxSpin);
        }

        // 수행 장수 목록(클릭 = 실행 컨펌).
        _detailCol.AddChild(new Label { Text = "수행 장수" });
        var generals = _session.AvailableGenerals(city).ToList();
        if (generals.Count == 0)
        {
            _detailCol.AddChild(new Label { Text = "(가능한 장수 없음)" });
        }

        foreach (var gid in generals)
        {
            var name = _session.State.Generals.First(g => g.Id == gid).Name;
            var btn = new Button { Text = name, CustomMinimumSize = new Vector2(0, 32) };
            var g = gid;
            btn.Pressed += () => OnGeneralClicked(city, cmdIndex, g);
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
            "troop" => _troops[System.Math.Max(0, _troopSel?.Selected ?? 0)].Code,
            "wall" => FactionResearch.WallCode,
            _ => "",
        };
        var facility = cmd.Param == "facility" ? Facilities[System.Math.Max(0, _facilitySel?.Selected ?? 0)].Code : "";
        var value = cmd.Param == "tax" ? (int)(_taxSpin?.Value ?? 0) : 0;

        _pending = new CommandRequest(city, cmd.Kind, general, Value: value, Facility: facility, TroopCode: troopCode);

        var cityName = _session.State.Cities.First(x => x.Id == city).Name;
        var generalName = _session.State.Generals.First(g => g.Id == general).Name;
        var paramText = cmd.Param switch
        {
            "troop" => $" · {_troops[System.Math.Max(0, _troopSel?.Selected ?? 0)].Name}",
            "facility" => $" · {Facilities[System.Math.Max(0, _facilitySel?.Selected ?? 0)].Label}",
            "tax" => $" · {value}%",
            _ => "",
        };
        _pendingLabel = $"{cmd.Label}{paramText}";
        _confirm.DialogText = $"{cityName} — {_pendingLabel}\n수행 장수: {generalName}\n\n실행하시겠습니까?";
        _confirm.PopupCentered();
    }

    private void OnConfirmed()
    {
        if (_pending is not { } request)
        {
            return;
        }

        var result = _session.Issue(request);
        _result.Text = result.Ok
            ? $"발행: {_pendingLabel} — {_session.State.Generals.First(g => g.Id == request.Main).Name}"
            : $"실패: {result.Error}";
        _pending = null;

        _cmdPanel.Visible = false;
        _detailPanel.Visible = false;
        _city = null;
        RebuildCities();
    }

    private static void Clear(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static string Size(CastleSize castle) => castle switch
    {
        CastleSize.Large => "대",
        CastleSize.Medium => "중",
        _ => "소",
    };
}
