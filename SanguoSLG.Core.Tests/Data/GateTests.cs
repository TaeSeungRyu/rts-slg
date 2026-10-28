namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class GateTests
{
    [Fact]
    public void CapacityIncludesWoundedAndRejectsWholeExcessUnit()
    {
        var gate = new Gate(7, "관문", new HexCoord(1, 1), GateSize.Small,
            new FactionId(1), 2_000, 0, Garrison:
            [new GateGarrison(new UnitId(1), "infantry", 96_000, 1_000, new GeneralId(1))]);
        Assert.True(gate.CanEnter(3_000));
        Assert.False(gate.CanEnter(4_000));
        Assert.False(gate.CanEnter(2_000, 1_001));
        Assert.Equal(1_500, gate.DailyProvisionUse(3_000));
        Assert.Equal(1_501, gate.DailyProvisionUse(3_001));
        Assert.Equal(2_000, gate.MaxIntegrity);
        Assert.Equal(3_000, (gate with { Size = GateSize.Medium }).MaxIntegrity);
    }

    [Fact]
    public void PresetGateSurvivesSaveAndInitialStateSnapshot()
    {
        var preset = new ScenarioMapPreset(1, 4, 3, ["PPPP", "PPPP", "PPPP"],
        [new MapEditorSite(1, "장안", "city", "small", 0, 0),
            new MapEditorSite(9, "함곡관", "gate", "medium", 2, 1, 3)]);
        preset.Validate();
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var scenario = ScenarioMapPresetApplier.Apply(source, preset);
        var game = GameState.FromScenario(scenario);
        var gate = Assert.Single(game.Cities, city => city.IsGate);
        Assert.Equal("함곡관", gate.Name);
        Assert.Equal(3, gate.Direction);
        Assert.Equal(3_000, gate.Wall);
        Assert.Equal(2, CastleFootprint.TilesFor(gate).Count());
        Assert.Equal("gate", game.MapPresetSnapshot!.Sites[1].Kind);
    }

    [Fact]
    public void PresetGateRejectsWaterAndOverlap()
    {
        var water = new ScenarioMapPreset(1, 3, 2, ["PPP", "PWP"],
            [new MapEditorSite(1, "수문", "gate", "small", 1, 1)]);
        Assert.Throws<InvalidDataException>(water.Validate);
        Assert.Throws<InvalidDataException>(() => (water with
        {
            Rows = ["PPP", "PPP"],
            Sites = [new MapEditorSite(1, "관문", "gate", "large", 1, 1)]
        }).Validate());
    }

    [Fact]
    public void MediumGateOccupiesTwoRotatedTiles()
    {
        var preset = new ScenarioMapPreset(1, 5, 4, Enumerable.Repeat("PPPPP", 4).ToArray(),
            [new MapEditorSite(1, "중형", "gate", "medium", 1, 1, 0),
                new MapEditorSite(2, "겹침", "gate", "small", 2, 2)]);
        Assert.Throws<InvalidDataException>(preset.Validate);
    }

    [Fact]
    public void GateGarrisonConsumesHalfFoodAndDoesNotProduceOrRecover()
    {
        var gate = new City(new CityId(1), "관문", new HexCoord(1, 1), new FactionId(1),
            100, Gold: 40, Wall: 2_000, Gate: GateSize.Small);
        var state = new GameState(1, 190, [], [gate], [],
            GarrisonForces: [new GarrisonForce(gate.Id, "swordsman", 10_000, 50)],
            CityWoundedForces: [new CityWoundedForce(gate.Id, "swordsman", 100, 50)]);
        var next = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0)).AdvanceDays(state, 7);
        Assert.Equal(65, next.Cities[0].Provisions);
        Assert.Equal(40, next.Cities[0].Gold);
        Assert.Equal(10_000, next.Garrisons.Single().Troops);
        Assert.Equal(100, next.CityWounded.Single().Troops);
    }

    [Fact]
    public void GateAcceptsCommanderButRejectsOrdinaryCityCommands()
    {
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var officer = source.Generals[0];
        var faction = source.Factions[0];
        var gate = new City(new CityId(911), "관문", new HexCoord(1, 1), faction.Id,
            100, Wall: 2_000, Gate: GateSize.Small);
        var state = new GameState(1, 190, [faction], [gate], [officer],
            Postings: [new GeneralPosting(officer.Id, faction.Id, gate.Id)]);
        var service = new CommandService(new CommandBalance());
        Assert.False(service.Issue(state,
            new CommandRequest(gate.Id, CommandKind.Research, officer.Id)).Ok);
        var appointment = service.Issue(state,
            new CommandRequest(gate.Id, CommandKind.AppointGovernor, officer.Id));
        Assert.True(appointment.Ok);
        Assert.Equal(officer.Id, appointment.State.Cities[0].Governor);
        var dismissed = GateService.DismissCommander(appointment.State, faction.Id, gate.Id);
        Assert.True(dismissed.Ok);
        Assert.Null(dismissed.State.Cities[0].Governor);
        Assert.False(GateService.DismissCommander(appointment.State, new FactionId(2), gate.Id).Ok);
    }

    [Theory]
    [InlineData(3_000, true)]
    [InlineData(4_000, false)]
    public void GateEntryIsAllOrNothing(int incoming, bool admitted)
    {
        var gate = new City(new CityId(1), "관문", new HexCoord(3, 0), new FactionId(1),
            1_000, Wall: 2_000, Gate: GateSize.Small);
        var field = new FieldUnit(new UnitId(2), gate.Owner, new HexCoord(1, 0),
            2, 1, 1, MovementDomain.Land, UnitMode.March, gate.Position, 2, 1);
        var unit = new CombatUnit(field, new CombatStats(100, 100, 100),
            new TroopPool(incoming, 0), UnitCombatState.Create(60), MaxTroops: incoming,
            TroopCode: "swordsman");
        var map = new HexMap(0, 8, -3, 3);
        var movement = new MovementSimulator(new PassabilityMap(map, [], [gate]));
        var advance = new AdvanceOrchestrator(movement,
            new CombatPhaseResolver(new BattleResolver(60), 70));
        var engine = new CampaignEngine(advance,
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0)));
        var state = new GameState(1, 190, [], [gate], [],
            GarrisonForces: [new GarrisonForce(gate.Id, "swordsman", 97_000, 50)],
            FieldArmies: [unit]);

        var result = engine.AdvanceWeek(state, out _);

        Assert.Equal(97_000 + (admitted ? incoming : 0),
            result.Garrisons.Where(force => force.City == gate.Id).Sum(force => force.Troops));
        Assert.Equal(!admitted, result.Armies.Any(army => army.Id == unit.Id));
    }

    [Fact]
    public void GateUsesSiegeDamageAndChangesOwnerImmediatelyWhenEmpty()
    {
        var gate = new City(new CityId(2), "적 관문", new HexCoord(3, 0),
            new FactionId(2), 0, Wall: 2_000, Gate: GateSize.Small);
        var field = new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(2, 0),
            1, 1, 1, MovementDomain.Land, UnitMode.Attack, gate.Position, 1, 1);
        var attacker = new CombatUnit(field, new CombatStats(8_000, 100, 100),
            new TroopPool(8_000, 0), UnitCombatState.Create(60), MaxTroops: 8_000,
            TroopCode: "swordsman");
        var state = new GameState(1, 190, [], [gate], [], FieldArmies: [attacker]);
        var templates = new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
        var map = new HexMap(0, 8, -3, 3);
        var advance = new AdvanceOrchestrator(
            new MovementSimulator(new PassabilityMap(map, [], [gate])),
            new CombatPhaseResolver(new BattleResolver(60), 70));
        var engine = new CampaignEngine(advance,
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0)),
            new CampaignSiege(new BattleResolver(60), templates));

        var after = engine.AdvanceWeek(state, out _, out var sieges);

        Assert.NotEmpty(sieges);
        Assert.True(after.Cities.Single().Wall < 2_000);
        var emptyGate = gate with { Wall = 0 };
        var captured = new CityCapture().ResolveAll(state with { Cities = [emptyGate] },
            new SeededRandomSource(2), out var reports);
        Assert.NotEmpty(reports);
        Assert.Equal(new FactionId(1), captured.Cities.Single().Owner);
        Assert.Equal(GateSize.Small, captured.Cities.Single().Gate);
    }
}
