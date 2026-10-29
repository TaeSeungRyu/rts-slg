namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;

public sealed class GateTests
{
    [Fact]
    public void FriendlyAndAlliedUnitsCanPassGateButEnemiesCannot()
    {
        var gate = new HexCoord(0, 0);
        var owner = new FactionId(1);
        var ally = new FactionId(2);
        var enemy = new FactionId(3);
        var map = new RenewalMovementMap(new HexMap(-3, 3, -3, 3), gateTiles:
            new Dictionary<HexCoord, FactionId> { [gate] = owner },
            areAllied: (a, b) => a == b || a == ally && b == owner || a == owner && b == ally);
        Assert.True(map.CanEnter(MovementDomain.Land, gate, owner));
        Assert.True(map.CanEnter(MovementDomain.Land, gate, ally));
        Assert.False(map.CanEnter(MovementDomain.Land, gate, enemy));
        Assert.False(map.CanEnter(MovementDomain.DeepWater, gate, owner));
        var from = RenewalHexSpace.Center(new HexCoord(-1, 0));
        var to = RenewalHexSpace.Center(new HexCoord(1, 0));
        Assert.Equal(RenewalStopReason.None, map.FirstStaticCollision(from, to, 0,
            MovementDomain.Land, ally));
        Assert.Equal(RenewalStopReason.BuildingBlocked, map.FirstStaticCollision(from, to, 0,
            MovementDomain.Land, enemy));
    }

    [Fact]
    public void CampaignMovementCrossesAlliedGateWithoutEnteringIt()
    {
        var gate = new City(new CityId(9), "동맹 관문", new HexCoord(2, 0),
            new FactionId(2), 100, Wall: 2_000, Gate: GateSize.Small);
        var field = new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(0, 0),
            3, 1, 1, MovementDomain.Land, UnitMode.March, new HexCoord(4, 0), 1, 1);
        var unit = new CombatUnit(field, new CombatStats(100, 100, 100),
            new TroopPool(2_000, 0), UnitCombatState.Create(60), MaxTroops: 2_000,
            TroopCode: "swordsman");
        var state = new GameState(1, 190, [], [gate], [], FieldArmies: [unit],
            FactionAlliances: [new FactionAlliance(new FactionId(1), gate.Owner, 1, 100)]);
        var map = new HexMap(-2, 6, -3, 3);
        var runner = new RenewalCampaignAdvanceRunner(map, new AdvanceOrchestrator(
            new MovementSimulator(new PassabilityMap(map, [], [gate])),
            new CombatPhaseResolver(new BattleResolver(60), 70)));
        runner.SetCampaignState(state);

        var turn = runner.Run([unit], maxDays: 2,
            castles: [new SiegeSite(gate.Position, gate.Owner,
                CastleFootprint.TilesFor(gate).ToList(), gate.Id)]);

        Assert.Empty(turn.EnteredCastle);
        Assert.True(Assert.Single(turn.Units).Field.Position.Q >= gate.Position.Q);
    }

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
    public void GateGarrisonDesertsWithoutFoodButNotWhileSupplied()
    {
        var gate = new City(new CityId(1), "관문", new HexCoord(1, 1), new FactionId(1),
            0, Wall: 2_000, Gate: GateSize.Small);
        var state = new GameState(1, 190, [], [gate], [],
            GarrisonForces: [new GarrisonForce(gate.Id, "swordsman", 10_000, 50)]);
        var engine = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0));

        var hungry = engine.AdvanceDays(state, 1);
        Assert.Equal(9_500, hungry.Garrisons.Single().Troops);
        Assert.Empty(hungry.CityWounded);

        var supplied = engine.AdvanceDays(state with
            { Cities = [gate with { Provisions = 100 }] }, 1);
        Assert.Equal(10_000, supplied.Garrisons.Single().Troops);
        Assert.Equal(95, supplied.Cities.Single().Provisions);
    }

    [Fact]
    public void LowSecurityGateDoesNotSpawnBandits()
    {
        var gate = new City(new CityId(1), "관문", new HexCoord(1, 1), new FactionId(1),
            100, Security: 0, Gate: GateSize.Small);
        var state = new GameState(1, 190, [], [gate], []);
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0));

        var next = world.AdvanceDays(state, 7);

        Assert.Empty(next.Armies);
        Assert.DoesNotContain(world.LastEvents, e => e.Kind == WorldEventKind.BanditRaid);
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

    [Fact]
    public void GateDeployUsesNormalCombatCompositionAndTransfersStoredResources()
    {
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var officer = source.Generals[0];
        var gate = new City(new CityId(911), "관문", new HexCoord(2, 2),
            source.Factions[0].Id, 120, Gold: 50, Wall: 2_000, Gate: GateSize.Small);
        var state = new GameState(1, 190, [source.Factions[0]], [gate], [officer],
            GarrisonForces: [new GarrisonForce(gate.Id, "swordsman", 5_000, 60)],
            Postings: [new GeneralPosting(officer.Id, gate.Owner, gate.Id)]);
        var deployer = new DeployService(new CommandBalance(),
            new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()),
            new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
            new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()));

        var result = deployer.Deploy(state, new DeployRequest(gate.Id, "swordsman", 2_000,
            officer.Id, Target: new HexCoord(5, 2), Provisions: 20, Gold: 10));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(3_000, result.State.Garrisons.Single().Troops);
        Assert.Equal(100, result.State.Cities.Single().Provisions);
        Assert.Equal(40, result.State.Cities.Single().Gold);
        Assert.Equal(gate.Id, Assert.Single(result.State.Armies).OriginCity);
    }

    [Theory]
    [InlineData(GateSize.Small, 2_000)]
    [InlineData(GateSize.Medium, 3_000)]
    public void GateRepairConsumesOwnGoldAndCompletesAtGateIntegrity(GateSize size, int maximum)
    {
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var officer = source.Generals[0];
        var gate = new City(new CityId(911), "관문", new HexCoord(2, 2), source.Factions[0].Id,
            80, Gold: 1_000, Wall: maximum - 500, Gate: size);
        var state = new GameState(1, 190, [source.Factions[0]], [gate], [officer],
            Postings: [new GeneralPosting(officer.Id, gate.Owner, gate.Id)]);
        var balance = new BalanceConfig(MonthlyTaxPerCity: 0);
        var rules = new CommandBalance();
        var request = new CommandRequest(gate.Id, CommandKind.Repair, officer.Id,
            TroopCode: FactionResearch.WallCode);

        var issued = new CommandService(rules, economy: balance).Issue(state, request);

        Assert.True(issued.Ok, issued.Error);
        Assert.True(issued.State.Cities.Single().Gold < gate.Gold);
        Assert.Equal(80, issued.State.Cities.Single().Provisions);
        var done = new WorldEngine(balance, rules).AdvanceDays(issued.State, rules.WallRepairDays);
        Assert.InRange(done.Cities.Single().Wall, gate.Wall + 1, maximum);
        Assert.Empty(done.Commands);
        Assert.False(new CommandService(rules, economy: balance).Issue(
            state with { Cities = [gate with { Wall = maximum }] }, request).Ok);
        Assert.False(new CommandService(rules, economy: balance).Issue(state,
            request with { TroopCode = "", Facility = "paddy" }).Ok);
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
