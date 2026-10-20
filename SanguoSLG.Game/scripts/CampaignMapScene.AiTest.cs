using System.Linq;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private static AiTestCase _selectedAiTestCase = ReadRequestedAiTestCase();
    private static bool _aiTestCaseSwitchInProgress;
    private OptionButton? _aiTestCaseSelector;

    private static AiTestCase ReadRequestedAiTestCase()
    {
        var value = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--aitestcase="));
        return int.TryParse(value?["--aitestcase=".Length..], out var selected)
            && System.Enum.IsDefined(typeof(AiTestCase), selected)
                ? (AiTestCase)selected : AiTestCase.Balanced;
    }

    private void AddAiTestCaseSelector(CanvasLayer layer)
    {
        if (_aiTestFixture is null) return;

        var panel = new PanelContainer
        {
            OffsetLeft = 10f,
            OffsetRight = 312f,
            OffsetTop = 112f,
            OffsetBottom = 172f,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 1, 8, 10));
        layer.AddChild(panel);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        panel.AddChild(row);
        row.AddChild(MakeLabel("AI 병력", 14, GoldBright));

        var selector = MakeOption(192);
        selector.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        selector.AddItem("1. 기존 병력", (int)AiTestCase.Balanced);
        selector.AddItem("2. 아군 절반", (int)AiTestCase.PlayerHalf);
        selector.AddItem("3. 적군 절반", (int)AiTestCase.EnemiesHalf);
        selector.Select((int)_selectedAiTestCase);
        selector.TooltipText = "조건 변경 시 진행·예약을 지우고 첫날부터 다시 시작합니다.";
        selector.ItemSelected += index => RequestAiTestCase((AiTestCase)selector.GetItemId((int)index));
        row.AddChild(selector);
        _aiTestCaseSelector = selector;
    }

    private void RequestAiTestCase(AiTestCase testCase)
    {
        if (_aiTestCaseSelector is null) return;
        _aiTestCaseSelector.Select((int)_selectedAiTestCase);
        if (testCase == _selectedAiTestCase) return;
        if (_advancing)
        {
            ShowNotice("조건 변경 불가", "진행이 끝난 뒤 병력 조건을 변경해 주세요.");
            return;
        }
        ShowConfirm("AI 검증 조건 변경",
            "진행 내용과 출전 예약을 버리고 첫날부터 다시 시작합니다. 병력 조건을 바꾸시겠습니까?",
            () =>
            {
                _selectedAiTestCase = testCase;
                GetTree().CallDeferred("reload_current_scene");
            });
    }

    private void RunAiTestCaseSwitchQa()
    {
        if (!_aiTestCaseSwitchInProgress)
        {
            _aiTestCaseSwitchInProgress = true;
            RequestAiTestCase(AiTestCase.PlayerHalf);
            var confirm = _confirmLayer?.FindChildren("*", "Button", true, false)
                .OfType<Button>().FirstOrDefault(button => button.Text == "확인");
            if (confirm is null)
            {
                GD.Print("[aitestcaseswitchqa] passed=False confirmMissing=True");
                GetTree().Quit(1);
                return;
            }
            confirm.EmitSignal(Button.SignalName.Pressed);
            return;
        }

        var passed = _aiTestFixture is not null
            && _selectedAiTestCase == AiTestCase.PlayerHalf
            && _aiTestCaseSelector?.Selected == (int)AiTestCase.PlayerHalf
            && _state.Garrisons.SequenceEqual(_aiTestFixture.GarrisonsFor(AiTestCase.PlayerHalf, Player))
            && _state.Day == 1;
        GD.Print($"[aitestcaseswitchqa] passed={passed} case={_selectedAiTestCase} day={_state.Day}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void RunAiTestFixtureQa()
    {
        var baselineMap = BuildTestMap();
        var mapReady = _aiTestFixture is not null && _map.Count > baselineMap.Count
            && _map.Tiles().Count(tile => _map.TerrainAt(tile) == TerrainType.WaterShallow) >= 100;
        var threeFactions = _state.Factions.Count == 3
            && _state.Factions.Select(f => f.Id).Distinct().Count() == 3
            && _state.Factions.All(f => _state.Cities.Count(c => c.Owner == f.Id) >= 3
                && _state.Cities.Any(c => c.Owner == f.Id && c.IsPort)
                && _state.Garrisons.Any(g => _state.Cities.Any(c => c.Id == g.City && c.Owner == f.Id))
                && _state.Assignments.Count(p => p.Faction == f.Id) >= 6);
        var generalsValid = _state.Assignments.All(p =>
            _state.Generals.Any(g => g.Id == p.General)
            && _state.Cities.Any(c => c.Id == p.Location && c.Owner == p.Faction));
        var casesValid = _aiTestCaseSelector?.ItemCount == 3;
        if (_aiTestFixture is not null)
        {
            var original = _aiTestFixture.Garrisons;
            var playerHalf = _aiTestFixture.GarrisonsFor(AiTestCase.PlayerHalf, Player);
            var enemiesHalf = _aiTestFixture.GarrisonsFor(AiTestCase.EnemiesHalf, Player);
            casesValid &= _state.Garrisons.SequenceEqual(
                _aiTestFixture.GarrisonsFor(_selectedAiTestCase, Player));
            casesValid &= original.Count == playerHalf.Count && original.Count == enemiesHalf.Count;
            for (var index = 0; index < original.Count; index++)
            {
                var playerOwned = _aiTestFixture.Cities.Single(city => city.Id == original[index].City).Owner == Player;
                casesValid &= playerHalf[index].Troops == original[index].Troops / (playerOwned ? 2 : 1);
                casesValid &= enemiesHalf[index].Troops == original[index].Troops / (playerOwned ? 1 : 2);
            }
        }
        var aiActs = false;
        if (mapReady && threeFactions && generalsValid)
        {
            var planned = _state;
            foreach (var faction in planned.Factions.Where(f => f.Id != Player))
                planned = _ai.PlanWeek(planned, faction.Id);
            aiActs = planned.Armies.Count > _state.Armies.Count
                || planned.Commands.Count > _state.Commands.Count;
        }
        var fullVision = _aiTestFixture is not null && _visibleTiles.SetEquals(_map.Tiles());
        var fullIntel = _aiTestFixture is not null && _state.Factions.All(faction =>
            _state.Cities.Where(city => city.Owner != faction.Id)
                .All(city => _state.IsScouted(faction.Id, city.Id)));
        var passed = mapReady && threeFactions && generalsValid && casesValid && aiActs && fullVision && fullIntel;
        GD.Print($"[aitestfixtureqa] passed={passed} map={_map.Count}/{baselineMap.Count} "
            + $"factions={_state.Factions.Count} cities={_state.Cities.Count} "
            + $"ports={_state.Cities.Count(c => c.IsPort)} generalsValid={generalsValid} "
            + $"case={_selectedAiTestCase} casesValid={casesValid} aiActs={aiActs} fullVision={fullVision} fullIntel={fullIntel}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
