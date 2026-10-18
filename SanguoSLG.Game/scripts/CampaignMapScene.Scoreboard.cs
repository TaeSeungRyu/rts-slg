using System;
using System.Linq;
using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private double _playTimeFraction;

    private void TickCampaignPlayTime(double delta)
    {
        _playTimeFraction += delta;
        var seconds = (int)_playTimeFraction;
        if (seconds <= 0 || _state is null) return;
        _playTimeFraction -= seconds;
        _state = _state with { PlaySeconds = _state.PlaySeconds + seconds };
        if (_advancing && _pendingState is not null)
            _pendingState = _pendingState with { PlaySeconds = _pendingState.PlaySeconds + seconds };
    }

    private void OpenFactionScoreboard()
    {
        if (_modalLayer is not null) { FadeOutModal(_modalLayer); _modalLayer = null; }
        const float width = 660;
        const float height = 640;
        var box = SystemView("세력 기록", width, out var scroll, out var panel, out var titleRow);
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        var owned = _state.Cities.Where(city => city.Owner == Player).ToArray();
        var ownedIds = owned.Select(city => city.Id).ToHashSet();
        var history = _state.Scoreboard.FirstOrDefault(row => row.Faction == Player);
        var researchLevels = _state.Research.Where(track => track.Faction == Player).Sum(track => track.Level);
        var garrison = _state.Garrisons.Where(force => ownedIds.Contains(force.City)).Sum(force => force.Troops);
        var field = _state.Armies.Where(unit => unit.Field.Owner == Player).Sum(unit => unit.Pool.Active);
        var wounded = _state.CityWounded.Where(force => ownedIds.Contains(force.City)).Sum(force => force.Troops)
            + _state.Armies.Where(unit => unit.Field.Owner == Player).Sum(unit => unit.Pool.Wounded);
        var weights = CampaignScoreWeights.LoadFromDirectory(_dataDirectory);
        var score = owned.Length * (long)weights.City + researchLevels * (long)weights.ResearchLevel
            + (history?.CapturedCities ?? 0) * (long)weights.CapturedCity
            + (history?.ProducedTroops ?? 0) / weights.TroopsPerPoint
            + (history?.SuccessfulStratagems ?? 0) * (long)weights.Stratagem
            + (history?.RuinDefenderStrength ?? 0) / weights.RuinStrengthDivisor
            + garrison / weights.TroopsPerPoint + field / weights.TroopsPerPoint
            - (history?.BattleFatalitiesEstimated ?? 0) / weights.FatalitiesPerPoint;
        var scoreText = MakeLabel($"전략 점수  {Math.Max(0, score):N0}", 26, GoldBright);
        scoreText.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(scoreText);
        box.AddChild(MakeLabel("점령·연구·병력은 현재 상태, 누적 수치는 기록 시작 이후 기준입니다.", 13, Parchment));
        box.AddChild(GoldRule());
        var table = new GridContainer { Name = "FactionStatisticsTable", Columns = 2 };
        table.AddThemeConstantOverride("h_separation", 24);
        table.AddThemeConstantOverride("v_separation", 8);
        box.AddChild(table);
        var elapsed = TimeSpan.FromSeconds(_state.PlaySeconds);
        var rows = new (string Label, string Value)[]
        {
            ("게임 플레이 시간", $"{(int)elapsed.TotalHours}시간 {elapsed.Minutes}분"),
            ("게임 진행 일수", $"{_state.Day:N0}일"),
            ("점령 중인 성·항구", $"{owned.Length:N0}곳"),
            ("점령 중인 항구", $"{owned.Count(city => city.IsPort):N0}곳"),
            ("누적 점령", $"{history?.CapturedCities ?? 0:N0}곳"),
            ("전사 병력(전투 추정)", $"{history?.BattleFatalitiesEstimated ?? 0:N0}명"),
            ("생산 병력", $"{history?.ProducedTroops ?? 0:N0}명"),
            ("주둔 병력", $"{garrison:N0}명"),
            ("출전 병력", $"{field:N0}명"),
            ("부상 병력", $"{wounded:N0}명"),
            ("연구 레벨 합계", $"{researchLevels:N0}레벨"),
            ("완료한 연구", $"{history?.CompletedResearch ?? 0:N0}회"),
            ("성공한 계략", $"{history?.SuccessfulStratagems ?? 0:N0}회"),
            ("점령한 유적", $"{history?.RuinsCaptured ?? 0:N0}곳 · 점수 {(history?.RuinDefenderStrength ?? 0) / weights.RuinStrengthDivisor:N0}"),
            ("보유 금", $"{owned.Sum(city => (long)city.Gold):N0}"),
            ("보유 군량", $"{owned.Sum(city => (long)city.Provisions):N0}"),
        };
        foreach (var (label, value) in rows)
        {
            var name = MakeLabel(label, 15, Parchment);
            name.CustomMinimumSize = new Vector2(260, 26);
            table.AddChild(name);
            var amount = MakeLabel(value, 15, GoldBright);
            amount.HorizontalAlignment = HorizontalAlignment.Right;
            amount.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            table.AddChild(amount);
        }
        CenterAndDrag(panel, titleRow, width, height, box);
    }

    private void RunScoreboardUiQa()
    {
        OpenFactionScoreboard();
        var table = _modalLayer?.FindChild("FactionStatisticsTable", true, false) as GridContainer;
        var passed = table is not null && table.Columns == 2 && table.GetChildCount() == 32
            && _modalLayer?.FindChild("SystemViewClose", true, false) is Button;
        GD.Print($"[maptestscoreboardqa] rows={table?.GetChildCount() / 2 ?? 0} passed={passed}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
