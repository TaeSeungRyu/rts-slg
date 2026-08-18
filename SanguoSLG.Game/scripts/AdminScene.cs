using System.Linq;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

/// <summary>
/// 내정 전용 게임 씬 1단계 — 대시보드(읽기 + 진행). Core의 <see cref="AdminSession"/>을 호출하고
/// 결과만 화면에 반영한다(게임 규칙은 노드에 넣지 않는다 — CLAUDE.md). 플레이어 세력 도시 현황과
/// "진행(주)" 버튼만 있는 최소 화면. 명령 팔레트는 2단계.
/// </summary>
public sealed partial class AdminScene : Control
{
    private AdminSession _session = null!;
    private Label _title = null!;
    private Label _cities = null!;

    /// <summary>Core 세션을 세우고 UI를 만든다(데이터 디렉토리 주입).</summary>
    public void Build(string dataDirectory)
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(dataDirectory);
        var commandBalance = new CommandBalanceLoader().LoadFromDirectory(dataDirectory);
        var troops = new TroopTypeLoader().LoadFromDirectory(dataDirectory);
        var adminSkills = new AdminSkillLoader().LoadFromDirectory(dataDirectory);

        var player = scenario.Factions.OrderBy(f => f.Id.Value).First().Id;
        _session = new AdminSession(
            GameState.FromScenario(scenario), player,
            new CommandService(commandBalance, troops),
            new WorldEngine(scenario.Balance, commandBalance, adminSkills));

        BuildUi();
        Refresh();
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        margin.AddChild(box);

        _title = new Label { Text = "내정" };
        _title.AddThemeFontSizeOverride("font_size", 26);
        box.AddChild(_title);

        _cities = new Label { AutowrapMode = TextServer.AutowrapMode.Off };
        _cities.AddThemeFontSizeOverride("font_size", 16);
        box.AddChild(_cities);

        var advance = new Button { Text = "진행 (7일)", CustomMinimumSize = new Vector2(160, 44) };
        advance.Pressed += OnAdvance;
        box.AddChild(advance);
    }

    private void OnAdvance()
    {
        _session.AdvanceWeek();
        Refresh();
    }

    private void Refresh()
    {
        var s = _session.State;
        var faction = s.Factions.First(f => f.Id == _session.Player);
        _title.Text = $"내정 — {faction.Name}   {s.Year}년 {s.Month}월 {s.DayOfMonth}일 (누적 {s.Day}일)";

        var lines = _session.PlayerCities().Select(city =>
        {
            var garrison = s.Garrisons.Where(g => g.City == city.Id)
                .Select(g => $"{g.TroopCode} {g.Troops}");
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
