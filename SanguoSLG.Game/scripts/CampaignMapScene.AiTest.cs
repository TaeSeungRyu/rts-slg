using System.Linq;
using Godot;
using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private static AiTestCase _selectedAiTestCase = ReadRequestedAiTestCase();
    private static bool _aiTestCaseSwitchInProgress;
    private OptionButton? _aiTestCaseSelector;

    private void SeedAiTestConstructionBuilders()
    {
        foreach (var (cityId, officerId, position) in new[]
        {
            (new CityId(201), new GeneralId(20), new HexCoord(6, 3)),
            (new CityId(301), new GeneralId(30), new HexCoord(10, 2)),
        })
        {
            var result = _deployer.Deploy(_state, new DeployRequest(cityId, "swordsman",
                2_000, officerId, Mode: UnitMode.Standby, Gold: 300));
            if (!result.Ok)
                throw new System.InvalidOperationException($"AI 건축 검수 부대 출전 실패: {result.Error}");
            var id = result.State.Armies.Max(unit => unit.Id.Value);
            _state = result.State with { FieldArmies = result.State.Armies.Select(unit =>
                unit.Id.Value == id ? unit with
                {
                    Field = unit.Field with { Position = position },
                    RenewalPosition = RenewalHexSpace.Center(position),
                    DeploymentDelayDays = 0,
                    AwaitingEgress = false,
                    EgressExit = null,
                } : unit).ToList() };
        }
    }

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
            && _state.Garrisons.SequenceEqual(AiTestGarrisonsAfterBuilders(AiTestCase.PlayerHalf))
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
        var ramsPresent = _state.Factions.All(faction => _state.Garrisons.Any(garrison =>
            garrison.TroopCode == "thunder_cart" && garrison.Troops > 0
            && _state.Cities.Any(city => city.Id == garrison.City && city.Owner == faction.Id)));
        var ruinsValid = _aiTestFixture is not null && _state.Ruins.Count == 3
            && _state.Ruins.All(ruin => ruin.MaxDefenders == 15_000
                && _state.RuinStatus.Any(status => status.RuinId == ruin.Id
                    && status.Defenders == ruin.MaxDefenders)
                && _state.Cities.Any(city => !city.IsPort
                    && city.Position.Distance(ruin.Position) <= 3));
        var generalsValid = _state.Assignments.All(p =>
            _state.Generals.Any(g => g.Id == p.General)
            && (_state.Cities.Any(c => c.Id == p.Location && c.Owner == p.Faction)
                || p.Location is null && _state.Armies.Any(unit => unit.Field.Owner == p.Faction
                    && (unit.VanguardId == p.General || unit.AdjutantId == p.General))));
        var casesValid = _aiTestCaseSelector?.ItemCount == 3;
        if (_aiTestFixture is not null)
        {
            var original = _aiTestFixture.Garrisons;
            var playerHalf = _aiTestFixture.GarrisonsFor(AiTestCase.PlayerHalf, Player);
            var enemiesHalf = _aiTestFixture.GarrisonsFor(AiTestCase.EnemiesHalf, Player);
            casesValid &= _state.Garrisons.SequenceEqual(
                AiTestGarrisonsAfterBuilders(_selectedAiTestCase));
            casesValid &= original.Count == playerHalf.Count && original.Count == enemiesHalf.Count;
            for (var index = 0; index < original.Count; index++)
            {
                var playerOwned = _aiTestFixture.Cities.Single(city => city.Id == original[index].City).Owner == Player;
                casesValid &= playerHalf[index].Troops == original[index].Troops / (playerOwned ? 2 : 1);
                casesValid &= enemiesHalf[index].Troops == original[index].Troops / (playerOwned ? 1 : 2);
            }
        }
        var aiActs = false;
        var aiCombatDeploys = 0;
        var aiSupplyDeploys = 0;
        var constructionValid = false;
        var constructionCycleValid = false;
        var constructionLongRunValid = false;
        if (mapReady && threeFactions && generalsValid)
        {
            var planned = _state;
            foreach (var faction in planned.Factions.Where(f => f.Id != Player))
                planned = _ai.PlanWeek(planned, faction.Id);
            aiActs = planned.Armies.Count > _state.Armies.Count
                || planned.Commands.Count > _state.Commands.Count;
            aiCombatDeploys = planned.Armies.Count(u => u.Field.Owner != Player && !u.IsSupply);
            aiSupplyDeploys = planned.Armies.Count(u => u.Field.Owner != Player && u.IsSupply);
            constructionValid = planned.Buildings.Any(building => building.Owner != Player
                && planned.Armies.Any(unit => unit.Id == building.BuilderUnit && unit.IsConstructing));
            if (constructionValid)
            {
                var advanced = _engine.AdvanceWeek(planned, out _);
                constructionCycleValid = advanced.Day == planned.Day + 7
                    && advanced.Buildings.Any(building => building.Owner != Player
                        && building.IsCompleted(advanced.Day))
                    && advanced.Buildings.All(building => advanced.Armies.Any(unit =>
                        unit.Id == building.BuilderUnit) || building.IsCompleted(advanced.Day));
                constructionLongRunValid = constructionCycleValid;
                for (var week = 0; week < 16 && constructionLongRunValid; week++)
                {
                    foreach (var faction in advanced.Factions.Where(faction => faction.Id != Player))
                        advanced = _ai.PlanWeek(advanced, faction.Id);
                    constructionLongRunValid = advanced.Buildings
                        .Where(building => building.Owner != Player && !building.IsCompleted(advanced.Day))
                        .GroupBy(building => building.Owner).All(group => group.Count() <= 2)
                        && advanced.Buildings.Select(building => building.Position).Distinct().Count()
                            == advanced.Buildings.Count
                        && advanced.Armies.All(unit => unit.CarryingGold >= 0 && unit.Provisions >= -1);
                    if (constructionLongRunValid)
                        advanced = _engine.AdvanceWeek(advanced, out _);
                }
                constructionLongRunValid &= advanced.Day == planned.Day + 119;
            }
        }
        var fullVision = _aiTestFixture is not null && _visibleTiles.SetEquals(_map.Tiles());
        var fullIntel = _aiTestFixture is not null && _state.Factions.All(faction =>
            _state.Cities.Where(city => city.Owner != faction.Id)
                .All(city => _state.IsScouted(faction.Id, city.Id)));
        var factionColors = _state.Factions.All(faction =>
            MapRelationTextColor(faction.Id) == ArmyFactionColor(faction.Id)
            && _state.Cities.Where(city => city.Owner == faction.Id)
                .All(city => _cityLabels[city.Id.Value].Modulate == ArmyFactionColor(faction.Id)));
        var offenseValid = _selectedAiTestCase != AiTestCase.PlayerHalf
            || aiCombatDeploys >= 2 && aiSupplyDeploys >= 1;
        var passed = mapReady && threeFactions && ramsPresent && ruinsValid && generalsValid && casesValid && aiActs
            && fullVision && fullIntel && factionColors && offenseValid
            && constructionValid && constructionCycleValid && constructionLongRunValid;
        GD.Print($"[aitestfixtureqa] passed={passed} map={_map.Count}/{baselineMap.Count} "
            + $"factions={_state.Factions.Count} cities={_state.Cities.Count} "
            + $"ports={_state.Cities.Count(c => c.IsPort)} ramsPresent={ramsPresent} ruinsValid={ruinsValid} generalsValid={generalsValid} "
            + $"case={_selectedAiTestCase} casesValid={casesValid} aiActs={aiActs} "
            + $"combat={aiCombatDeploys} supply={aiSupplyDeploys} offenseValid={offenseValid} "
            + $"constructionValid={constructionValid} constructionCycle={constructionCycleValid} "
            + $"construction119Day={constructionLongRunValid} "
            + $"fullVision={fullVision} fullIntel={fullIntel} factionColors={factionColors}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void RunAiTestRuinQa()
    {
        var state = _state with { ScoutedCities = [] };
        var success = _aiTestFixture is not null && state.Ruins.Count == 3;
        foreach (var faction in state.Factions.Where(faction => faction.Id != Player))
        {
            state = _ai.PlanWeek(state, faction.Id);
            var target = state.Ruins.SingleOrDefault(ruin => state.Cities.Any(city =>
                city.Owner == faction.Id && !city.IsPort
                && city.Position.Distance(ruin.Position) <= 3));
            var expedition = state.Armies.Where(unit => unit.Field.Owner == faction.Id
                && unit.AiRuinTarget == target?.Id).ToList();
            success &= target is not null && expedition.Sum(unit => unit.Pool.Active) >= 30_000
                && expedition.All(unit => unit.Field.Target == target.Position);
        }
        var advanced = _engine.AdvanceWeek(state, out var turns);
        var exchanged = turns.SelectMany(turn => turn.RuinExchanges).Count();
        success &= advanced.Day == state.Day + 7 && exchanged > 0;
        GD.Print($"[aitestruinqa] passed={success} expeditions={state.Armies.Count(unit => unit.AiRuinTarget is not null)} exchanges={exchanged}");
        GetTree().Quit(success ? 0 : 1);
    }

    private void RunAiTestWarQa()
    {
        var timer = new System.Diagnostics.Stopwatch();
        var aiMilliseconds = 0L;
        var advanceMilliseconds = 0L;
        var maxAiMilliseconds = 0L;
        var state = _state;
        var initial = state.Day;
        var sorties = new Dictionary<FactionId, int>();
        var supportSorties = new Dictionary<FactionId, int>();
        var supportDelays = new Dictionary<FactionId, int>();
        var decisions = new Dictionary<FactionId, int>();
        var buildings = new Dictionary<FactionId, HashSet<FieldBuildingId>>();
        var clockValid = true;
        var verbose = OS.GetCmdlineUserArgs().Contains("--aitestwarqaverbose");
        for (var week = 0; week < 52; week++)
        {
            foreach (var faction in state.Factions.Where(entry => entry.Id != Player))
            {
                var before = state.Armies.Select(unit => unit.Id).ToHashSet();
                var priorDecision = state.Factions.First(entry => entry.Id == faction.Id)
                    .AiNextWarDecisionDay;
                var candidates = verbose
                    ? AiWarCandidatePolicy.Evaluate(state, faction.Id, _map, _balance, 1) : [];
                timer.Restart();
                state = _ai.PlanWeek(state, faction.Id);
                timer.Stop();
                aiMilliseconds += timer.ElapsedMilliseconds;
                maxAiMilliseconds = Math.Max(maxAiMilliseconds, timer.ElapsedMilliseconds);
                if (priorDecision == 0 || state.Day >= priorDecision)
                {
                    decisions[faction.Id] = decisions.GetValueOrDefault(faction.Id) + 1;
                    var nextDecision = state.Factions.First(entry => entry.Id == faction.Id)
                        .AiNextWarDecisionDay;
                    var supporting = state.Armies.Any(unit => unit.Field.Owner == faction.Id
                        && unit.AiSupportDefendedCity is not null && unit.Pool.Active > 0);
                    clockValid &= supporting ? nextDecision == state.Day + 7
                        : nextDecision - state.Day is >= 30 and <= 60;
                    if (supporting)
                        supportDelays[faction.Id] = supportDelays.GetValueOrDefault(faction.Id) + 1;
                }
                var newCombat = state.Armies.Where(unit => !before.Contains(unit.Id)
                    && unit.Field.Owner == faction.Id && !unit.IsSupply && !unit.IsTransport)
                    .ToList();
                sorties[faction.Id] = sorties.GetValueOrDefault(faction.Id) + newCombat.Count;
                supportSorties[faction.Id] = supportSorties.GetValueOrDefault(faction.Id)
                    + newCombat.Count(unit => unit.AiSupportDefendedCity is not null);
                if (!buildings.TryGetValue(faction.Id, out var started))
                    buildings[faction.Id] = started = [];
                foreach (var building in state.Buildings.Where(building => building.Owner == faction.Id))
                    started.Add(building.Id);
                if (verbose && (priorDecision == 0 || state.Day >= priorDecision
                    || newCombat.Count > 0))
                {
                    var fronts = string.Join(',', candidates.Take(5).Select(candidate =>
                    {
                        var origin = state.Cities.First(city => city.Id == candidate.Origin);
                        var target = state.Cities.First(city => city.Id == candidate.Target);
                        var force = AiWarForcePolicy.Evaluate(state, origin, target,
                            candidates, _balance);
                        return $"{origin.Name}>{target.Name}:{candidate.TravelDays}일"
                            + $"/{force.OriginAvailable}+{force.SupportAvailable}"
                            + $"대{force.DefenderTroops}/{force.RequiredRatioTenths}";
                    }));
                    var choices = string.Join(',', candidates.Select(candidate => candidate.Origin)
                        .Distinct().Select(originId =>
                        {
                            var origin = state.Cities.First(city => city.Id == originId);
                            var selected = AiWarCandidatePolicy.ChooseTargets(candidates,
                                state.Day, faction.Id, originId);
                            var free = state.GeneralsAt(originId).Count(id => !state.IsGeneralBusy(id));
                            var total = state.GeneralsAt(originId).Count();
                            var commands = string.Join('/', state.Commands.Where(command => command.City == originId)
                                .Select(command => $"{command.Kind}:{command.CompletionDay - state.Day}"));
                            return $"{origin.Name}[{free}/{total};{commands}]=>"
                                + string.Join('/', selected.Select(candidate => state.Cities
                                    .First(city => city.Id == candidate.Target).Name));
                        }));
                    GD.Print($"[aitestwarqa] day={state.Day} faction={faction.Name} "
                        + $"next={state.Factions.First(entry => entry.Id == faction.Id).AiNextWarDecisionDay} "
                        + $"candidates={candidates.Count} fronts={fronts} choices={choices} "
                        + $"occupied={AiWarForcePolicy.OccupiedTargets(state, faction.Id)} "
                        + $"buildDue={AiFieldConstructionEvaluator.PeriodicBuildDue(state, faction.Id)} "
                        + $"buildings={string.Join('/', state.Buildings.Where(building => building.Owner == faction.Id).Select(building => $"{building.DefinitionCode}:{building.StartedDay}:{building.Position}"))} "
                        + $"field={string.Join('/', state.Armies.Where(unit => unit.Field.Owner == faction.Id && !unit.IsSupply).Select(unit => $"{unit.Id.Value}:{unit.Field.Mode}:{unit.Pool.Active}:{unit.Field.Target}"))} "
                        + $"newCombat={newCombat.Count} total={sorties[faction.Id]}");
                }
            }
            timer.Restart();
            state = _engine.AdvanceWeek(state, out _);
            timer.Stop();
            advanceMilliseconds += timer.ElapsedMilliseconds;
        }
        var passed = clockValid && state.Day - initial == 364
            && state.Factions.Where(faction => faction.Id != Player)
                .All(faction => decisions.GetValueOrDefault(faction.Id) >= 5
                    && sorties.GetValueOrDefault(faction.Id) >= 10
                    && buildings.TryGetValue(faction.Id, out var started) && started.Count >= 2);
        GD.Print($"[aitestwarqa] summary case={_selectedAiTestCase} passed={passed} "
            + $"clock={clockValid} days={state.Day - initial} "
            + $"aiMs={aiMilliseconds} maxAiMs={maxAiMilliseconds} advanceMs={advanceMilliseconds} "
            + string.Join(' ', sorties.OrderBy(entry => entry.Key.Value)
                .Select(entry => $"faction{entry.Key.Value}=sorties:{entry.Value}"
                    + $",decisions:{decisions.GetValueOrDefault(entry.Key)}"
                    + $",support:{supportSorties.GetValueOrDefault(entry.Key)}"
                    + $",supportDelays:{supportDelays.GetValueOrDefault(entry.Key)}"
                    + $",buildings:{buildings[entry.Key].Count}")));
        GetTree().Quit(passed ? 0 : 1);
    }

    private void RunAiTestAdvanceProfileQa()
    {
        StartAdvance();
        GetTree().Quit(_advancing ? 0 : 1);
    }

    private IReadOnlyList<GarrisonForce> AiTestGarrisonsAfterBuilders(AiTestCase testCase)
        => _aiTestFixture!.GarrisonsFor(testCase, Player).Select(line =>
            line.TroopCode == "swordsman" && line.City.Value is 201 or 301
                ? line with { Troops = line.Troops - 2_000 } : line).ToList();
}
