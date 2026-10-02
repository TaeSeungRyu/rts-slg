namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public sealed class FieldConstructionServiceTests
{
    private static readonly FactionId Player = new(1);
    private static readonly IReadOnlyList<FieldBuildingDefinition> Definitions =
        new FieldBuildingLoader().LoadFromDirectory(TestData.DataDirectory());

    private static CombatUnit Unit(int troops = 1000, TroopClass troopClass = TroopClass.Infantry)
        => new(
            new FieldUnit(new UnitId(1), Player, new HexCoord(2, 2), 2, 2, 1,
                MovementDomain.Land, UnitMode.Attack, new HexCoord(8, 2), 1,
                Waypoints: [new HexCoord(4, 2)]),
            new CombatStats(troops, 10, 8), new TroopPool(troops, 30), UnitCombatState.Create(60),
            MaxTroops: 10_000, Class: troopClass, Provisions: 100, LootGold: 40, CargoGold: 100);

    private static FieldConstructionService Service(TerrainType terrain = TerrainType.Plains)
        => new(Definitions, _ => terrain);

    [Fact]
    public void 병력_천명_경계와_해상부대를_검증한다()
    {
        var below = State(Unit(999));
        Assert.False(Service().Start(below, Player,
            new(new UnitId(1), "palisade", new HexCoord(2, 2))).Ok);

        var exact = Service().Start(State(Unit()), Player,
            new(new UnitId(1), "palisade", new HexCoord(2, 2)));
        Assert.True(exact.Ok, exact.Error);

        var naval = Service().Start(State(Unit(troopClass: TroopClass.Naval)), Player,
            new(new UnitId(1), "palisade", new HexCoord(2, 2)));
        Assert.False(naval.Ok);
    }

    [Fact]
    public void 착공은_휴대자원을_한번만_차감하고_타일을_예약한다()
    {
        var started = Service(TerrainType.Forest).Start(State(Unit()), Player,
            new(new UnitId(1), "scout_post", new HexCoord(3, 2)));

        Assert.True(started.Ok, started.Error);
        var unit = Assert.Single(started.State.Armies);
        Assert.Equal(990, unit.Pool.Active);
        Assert.Equal(95, unit.Provisions);
        Assert.Equal(90, unit.CargoGold);
        Assert.Equal(40, unit.LootGold);
        Assert.True(unit.IsConstructing);
        Assert.Null(unit.Field.Target);
        Assert.Null(unit.Field.Waypoints);
        var building = Assert.Single(started.State.Buildings);
        Assert.Equal(new HexCoord(3, 2), building.Position);
        Assert.Equal(new UnitId(1), building.BuilderUnit);
        Assert.Equal(8, building.CompletionDay);
        Assert.Equal(68, building.ExpiresDay);

        var duplicate = Service(TerrainType.Forest).Start(
            started.State with { FieldArmies = [Unit() with { Field = Unit().Field with { Id = new UnitId(2) } }] },
            Player, new(new UnitId(2), "scout_post", new HexCoord(3, 2)));
        Assert.False(duplicate.Ok);

        var command = new FieldUnitCommandService((_, _) => true).Reassign(started.State, Player,
            new FieldUnitCommandRequest(new UnitId(1), UnitMode.Attack, new HexCoord(4, 2)));
        Assert.False(command.Ok);
    }

    [Fact]
    public void 건축중인_부대는_이동_공격_반격_게이지충전을_하지않는다()
    {
        var builder = Unit() with
        {
            Field = Unit().Field with { Position = new HexCoord(2, 2), Target = new HexCoord(6, 2) },
        };
        var enemy = Unit() with
        {
            Field = Unit().Field with
            {
                Id = new UnitId(2), Owner = new FactionId(2), Position = new HexCoord(3, 2),
                Target = new HexCoord(2, 2), CommandOrder = 2,
            },
        };
        var turn = Orchestrator().Run([builder, enemy], maxDays: 1,
            constructionUnits: new HashSet<UnitId> { builder.Id });

        var result = turn.Units.Single(x => x.Id == builder.Id);
        Assert.Equal(new HexCoord(2, 2), result.Field.Position);
        Assert.Equal(0, turn.Combat?.DamageDealt.GetValueOrDefault(builder.Id));
        Assert.True(turn.Combat?.DamageTaken.GetValueOrDefault(builder.Id) > 0);
        Assert.Equal(0, result.State.SharedActiveGauge.ElapsedDays);
        Assert.Empty(turn.FiredActives);
    }

    [Fact]
    public void 완공일에는_부대가_명령대기로_풀리고_건축연결이_해제된다()
    {
        var unit = Unit();
        var state = State(unit) with
        {
            FieldBuildings =
            [
                new(new FieldBuildingId(1), "palisade", Player, unit.Field.Position, 250,
                    1, 8, BuilderUnit: unit.Id),
            ],
        };

        var next = Campaign().AdvanceWeek(state, out _);

        Assert.Equal(8, next.Day);
        Assert.Null(next.Buildings.Single().BuilderUnit);
        Assert.False(next.Armies.Single().IsConstructing);
        Assert.Null(next.Armies.Single().Field.Target);
        Assert.Equal(UnitMode.Advance, next.Armies.Single().Field.Mode);
    }

    [Fact]
    public void 건축부대가_괴멸해_사라졌으면_미완성_공사장도_제거된다()
    {
        var survivor = Unit() with { Field = Unit().Field with { Id = new UnitId(2), Target = null } };
        var orphan = new FieldBuilding(new FieldBuildingId(1), "palisade", Player, new HexCoord(3, 2),
            250, 1, 20, BuilderUnit: new UnitId(99));
        var state = State(survivor) with { FieldBuildings = [orphan] };

        var next = Campaign().AdvanceWeek(state, out _);

        Assert.Empty(next.Buildings);
        Assert.Single(next.Armies);
    }

    private static GameState State(CombatUnit unit)
        => new(1, 190, [], [], [], FieldArmies: [unit]);

    private static AdvanceOrchestrator Orchestrator()
    {
        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 10, 0, 10), [], []));
        return new AdvanceOrchestrator(movement,
            new CombatPhaseResolver(new BattleResolver(60), woundedPercent: 70));
    }

    private static CampaignEngine Campaign()
        => new(Orchestrator(), new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100)));
}
