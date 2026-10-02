namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class FieldFortTests
{
    private static readonly FactionId Player = new(1);
    private static readonly IReadOnlyList<FieldBuildingDefinition> Definitions =
        new FieldBuildingLoader().LoadFromDirectory(TestData.DataDirectory());

    private static FieldBuilding Fort(int id, HexCoord at, UnitId? garrison = null)
    {
        var definition = Definitions.Single(x => x.Code == "fort");
        return new(new FieldBuildingId(id), definition.Code, Player, at, definition.MaxHitPoints,
            StartedDay: 0, CompletionDay: 1, GarrisonUnit: garrison);
    }

    private static CombatUnit Unit(int id, HexCoord at, HexCoord? target = null, int provisions = 100)
    {
        var field = new FieldUnit(new UnitId(id), Player, at, 2, 2, 1,
            MovementDomain.Land, UnitMode.Advance, target, id, RangeCastle: 1);
        return new(field, new CombatStats(10_000, 20, 12), new TroopPool(10_000, 0),
            UnitCombatState.Create(60), MaxTroops: 10_000, Provisions: provisions);
    }

    private static AdvanceOrchestrator Orchestrator()
    {
        var movement = new MovementSimulator(new PassabilityMap(new HexMap(-5, 10, -5, 5), [], []));
        return new(movement, new CombatPhaseResolver(new BattleResolver(60), woundedPercent: 70));
    }

    [Fact]
    public void 완공된_보루를_목표로_도착하면_한_부대가_주둔한다()
    {
        var fort = Fort(1, new HexCoord(2, 0));
        var unit = Unit(1, default, fort.Position);

        var turn = Orchestrator().Run([unit], maxDays: 1,
            fieldBuildings: [fort], fieldDefinitions: Definitions, fieldDay: 1);

        Assert.Equal(unit.Id, turn.FieldGarrisons[fort.Id]);
        var garrison = Assert.Single(turn.Units);
        Assert.Equal(fort.Position, garrison.Field.Position);
        Assert.Null(garrison.Field.Target);
    }

    [Fact]
    public void 보루_반경의_군량절감은_두개여도_40퍼센트로_한번만_적용된다()
    {
        var moving = Unit(1, default, new HexCoord(4, 0));
        var oneFort = Fort(1, new HexCoord(2, 0));
        var secondFort = Fort(2, new HexCoord(2, -1));

        var one = Orchestrator().Run([moving], maxDays: 1,
            fieldBuildings: [oneFort], fieldDefinitions: Definitions, fieldDay: 1);
        var two = Orchestrator().Run([moving], maxDays: 1,
            fieldBuildings: [oneFort, secondFort], fieldDefinitions: Definitions, fieldDay: 1);

        Assert.Equal(94, one.Units.Single().Provisions);
        Assert.Equal(94, two.Units.Single().Provisions);
    }

    [Fact]
    public void 주둔중에는_철거할수없고_출성하면_다시_철거할수있다()
    {
        var unit = Unit(1, new HexCoord(2, 0));
        var fort = Fort(1, unit.Field.Position, unit.Id);
        var state = new GameState(1, 190, [], [], [], FieldArmies: [unit], FieldBuildings: [fort]);
        var service = new FieldConstructionService(Definitions, _ => TerrainType.Plains);

        var blocked = service.Demolish(state, Player, fort.Id);
        Assert.False(blocked.Ok);

        var exit = service.ExitGarrison(state, Player, fort.Id);
        Assert.True(exit.Ok);
        Assert.Null(exit.State.Buildings.Single().GarrisonUnit);
        Assert.True(service.Demolish(exit.State, Player, fort.Id).Ok);
    }

    [Fact]
    public void 보루가_남아있는동안_내부부대는_일반교전_피해를_받지않는다()
    {
        var garrison = Unit(1, new HexCoord(2, 0));
        var enemyField = new FieldUnit(new UnitId(2), new FactionId(2), new HexCoord(1, 0), 0, 2, 1,
            MovementDomain.Land, UnitMode.Attack, garrison.Field.Position, 2, RangeCastle: 1);
        var enemy = new CombatUnit(enemyField, new CombatStats(10_000, 20, 12), new TroopPool(10_000, 0),
            UnitCombatState.Create(60), MaxTroops: 10_000);
        var fort = Fort(1, garrison.Field.Position, garrison.Id);

        var turn = Orchestrator().Run([garrison, enemy], maxDays: 1,
            fieldBuildings: [fort], fieldDefinitions: Definitions, fieldDay: 1);

        Assert.Equal(10_000, turn.Units.Single(x => x.Id == garrison.Id).Pool.Active);
        var state = new GameState(1, 190, [], [], [], FieldArmies: turn.Units, FieldBuildings: [fort]);
        var buildingCombat = new FieldBuildingCombat(new BattleResolver(60), Definitions).Resolve(state, turn.Units);
        var exchange = Assert.Single(buildingCombat.Exchanges);
        Assert.True(exchange.Destroyed
            || buildingCombat.State.Buildings.Single().HitPoints < fort.HitPoints);
        Assert.Equal(10_000, buildingCombat.Armies.Single(x => x.Id == garrison.Id).Pool.Active);
    }
}
