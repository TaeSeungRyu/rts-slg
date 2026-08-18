using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

/// <summary>
/// 내정 전용 게임 씬(12b) — Core <see cref="AdminSession"/>을 호출하고 결과만 화면에 반영한다(게임
/// 규칙은 노드에 넣지 않는다 — CLAUDE.md). 1단계 대시보드(도시 현황·진행) + 2단계 명령 팔레트
/// (도시·명령·장수·파라미터 선택 → 발행). 전투 없음.
/// </summary>
public sealed partial class AdminScene : Control
{
    private AdminSession _session = null!;
    private IReadOnlyList<TroopTemplate> _troops = null!;

    private Label _title = null!;
    private Label _cities = null!;
    private Label _result = null!;

    private OptionButton _citySelect = null!;
    private OptionButton _cmdSelect = null!;
    private OptionButton _genSelect = null!;
    private OptionButton _troopSelect = null!;
    private OptionButton _facilitySelect = null!;
    private SpinBox _taxSpin = null!;

    private readonly List<CityId> _cityIds = new();
    private readonly List<GeneralId> _genIds = new();

    // 명령 목록(표시명, 종류, 파라미터 모드). 파라미터: troop=병종, facility=시설, tax=세율, wall=성벽연구, none.
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

    /// <summary>Core 세션을 세우고 UI를 만든다(데이터 디렉토리 주입).</summary>
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
        RefreshGenerals();
        UpdateParamVisibility();
        Refresh();
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 22);
        }

        AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        margin.AddChild(box);

        _title = new Label { Text = "내정" };
        _title.AddThemeFontSizeOverride("font_size", 24);
        box.AddChild(_title);

        _cities = new Label { AutowrapMode = TextServer.AutowrapMode.Off };
        _cities.AddThemeFontSizeOverride("font_size", 15);
        box.AddChild(_cities);

        var advance = new Button { Text = "진행 (7일)", CustomMinimumSize = new Vector2(150, 40) };
        advance.Pressed += OnAdvance;
        box.AddChild(advance);

        box.AddChild(new HSeparator());
        var palette = new Label { Text = "명령 팔레트" };
        palette.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(palette);

        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", 8);
        box.AddChild(row1);
        _citySelect = NewSelect(row1, 160);
        _cmdSelect = NewSelect(row1, 130);
        _genSelect = NewSelect(row1, 130);

        var row2 = new HBoxContainer();
        row2.AddThemeConstantOverride("separation", 8);
        box.AddChild(row2);
        _troopSelect = NewSelect(row2, 130);
        _facilitySelect = NewSelect(row2, 100);
        _taxSpin = new SpinBox { MinValue = 0, MaxValue = 50, Step = 5, Value = 20, CustomMinimumSize = new Vector2(90, 0) };
        row2.AddChild(_taxSpin);
        var issue = new Button { Text = "발행", CustomMinimumSize = new Vector2(90, 36) };
        issue.Pressed += OnIssue;
        row2.AddChild(issue);

        _result = new Label { Text = "" };
        _result.AddThemeFontSizeOverride("font_size", 15);
        box.AddChild(_result);

        // 선택지 채우기(도시·명령·병종·시설은 고정, 장수는 도시별 갱신).
        foreach (var city in _session.PlayerCities())
        {
            _citySelect.AddItem($"{city.Name}");
            _cityIds.Add(city.Id);
        }

        foreach (var c in Commands)
        {
            _cmdSelect.AddItem(c.Label);
        }

        foreach (var t in _troops)
        {
            _troopSelect.AddItem(t.Name);
        }

        foreach (var f in Facilities)
        {
            _facilitySelect.AddItem(f.Label);
        }

        _citySelect.ItemSelected += _ => RefreshGenerals();
        _cmdSelect.ItemSelected += _ => UpdateParamVisibility();
    }

    private static OptionButton NewSelect(Container parent, int minWidth)
    {
        var opt = new OptionButton { CustomMinimumSize = new Vector2(minWidth, 36) };
        parent.AddChild(opt);
        return opt;
    }

    private void OnAdvance()
    {
        _session.AdvanceWeek();
        RefreshGenerals();
        Refresh();
    }

    // 선택 도시에서 명령 가능한 장수(주둔·비잠금)로 장수 선택지를 채운다.
    private void RefreshGenerals()
    {
        _genSelect.Clear();
        _genIds.Clear();
        if (_citySelect.Selected < 0)
        {
            return;
        }

        var cityId = _cityIds[_citySelect.Selected];
        foreach (var gid in _session.AvailableGenerals(cityId))
        {
            var name = _session.State.Generals.First(g => g.Id == gid).Name;
            _genSelect.AddItem(name);
            _genIds.Add(gid);
        }

        if (_genIds.Count == 0)
        {
            _genSelect.AddItem("(가능한 장수 없음)");
        }
    }

    private void UpdateParamVisibility()
    {
        var param = _cmdSelect.Selected >= 0 ? Commands[_cmdSelect.Selected].Param : "troop";
        _troopSelect.Visible = param == "troop";
        _facilitySelect.Visible = param == "facility";
        _taxSpin.Visible = param == "tax";
    }

    private void OnIssue()
    {
        if (_citySelect.Selected < 0 || _cmdSelect.Selected < 0)
        {
            _result.Text = "도시·명령을 선택하세요.";
            return;
        }

        if (_genIds.Count == 0)
        {
            _result.Text = "명령을 수행할 장수가 없습니다(모두 출전·잠김).";
            return;
        }

        var city = _cityIds[_citySelect.Selected];
        var general = _genIds[System.Math.Max(0, _genSelect.Selected)];
        var cmd = Commands[_cmdSelect.Selected];

        var troopCode = cmd.Param switch
        {
            "troop" => _troops[System.Math.Max(0, _troopSelect.Selected)].Code,
            "wall" => FactionResearch.WallCode,
            _ => "",
        };
        var facility = cmd.Param == "facility" ? Facilities[System.Math.Max(0, _facilitySelect.Selected)].Code : "";
        var value = cmd.Param == "tax" ? (int)_taxSpin.Value : 0;

        var result = _session.Issue(new CommandRequest(city, cmd.Kind, general, Value: value, Facility: facility, TroopCode: troopCode));
        _result.Text = result.Ok
            ? $"발행: {cmd.Label} — {_session.State.Generals.First(g => g.Id == general).Name}"
            : $"실패: {result.Error}";

        RefreshGenerals();
        Refresh();
    }

    private void Refresh()
    {
        var s = _session.State;
        var faction = s.Factions.First(f => f.Id == _session.Player);
        _title.Text = $"내정 — {faction.Name}   {s.Year}년 {s.Month}월 {s.DayOfMonth}일 (누적 {s.Day}일)";

        var lines = _session.PlayerCities().Select(city =>
        {
            var garrison = s.Garrisons.Where(g => g.City == city.Id).Select(g => $"{g.TroopCode} {g.Troops}");
            var garrisonText = garrison.Any() ? string.Join(", ", garrison) : "없음";
            var facilities = $"논{city.Paddies} 밭{city.Farms} 마을{city.Villages}{(city.Workshop ? " 공방" : "")}";
            var pending = _session.PendingAt(city.Id).Count;
            var pendingText = pending > 0 ? $"  진행중 명령 {pending}" : "";

            return $"[{Size(city.Castle)}] {city.Name}\n" +
                $"    금 {city.Gold}  군량 {city.Provisions}  인구 {city.Population}  치안 {city.Security}  " +
                $"세율 {city.TaxRate}%  성벽 {city.Wall}\n" +
                $"    자원: 광석 {city.Ore} 말 {city.Horses} 코끼리 {city.Elephants}  |  시설: {facilities}\n" +
                $"    대기 병력: {garrisonText}{pendingText}";
        });

        _cities.Text = string.Join("\n\n", lines);
    }

    private static string Size(CastleSize castle) => castle switch
    {
        CastleSize.Large => "대",
        CastleSize.Medium => "중",
        _ => "소",
    };
}
