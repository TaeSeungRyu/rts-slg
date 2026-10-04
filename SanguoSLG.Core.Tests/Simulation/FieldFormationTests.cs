namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class FieldFormationTests
{
    private sealed class FixedRandom(int value) : IRandomSource
    {
        public int Calls { get; private set; }
        public int Next(int minInclusive, int maxExclusive) { Calls++; return value; }
    }

    private static readonly IReadOnlyList<FieldBuildingDefinition> Definitions =
        new FieldBuildingLoader().LoadFromDirectory(TestData.DataDirectory());

    private static FieldBuilding Formation(int id, HexCoord position)
    {
        var definition = Definitions.Single(x => x.Code == "formation");
        return new(new FieldBuildingId(id), definition.Code, new FactionId(1), position,
            definition.MaxHitPoints, 0, 1);
    }

    private static CombatUnit Unit(HexCoord position, HexCoord? target)
    {
        var field = new FieldUnit(new UnitId(1), new FactionId(2), position, 2, 2, 1,
            MovementDomain.Land, UnitMode.Advance, target, 1, RangeCastle: 1);
        return new(field, new CombatStats(10_000, 20, 12), new TroopPool(10_000, 250),
            UnitCombatState.Create(60), MaxTroops: 10_000);
    }

    private static AdvanceOrchestrator Orchestrator(IRandomSource random)
    {
        var movement = new MovementSimulator(new PassabilityMap(new HexMap(-5, 10, -5, 5), [], []));
        return new(movement, new CombatPhaseResolver(new BattleResolver(60), 70), random: random);
    }

    [Fact]
    public void 이동턴_종료시_진법_범위안이면_공격전에_현역_1퍼센트가_부상으로_전환된다()
    {
        var random = new FixedRandom(0);
        var formation = Formation(1, new HexCoord(2, 0));
        var turn = Orchestrator(random).Run([Unit(default, new HexCoord(4, 0))], maxDays: 1,
            fieldBuildings: [formation], fieldDefinitions: Definitions, fieldDay: 1);

        var trigger = Assert.Single(turn.FormationTriggers);
        Assert.True(trigger.Activated);
        Assert.Equal(100, trigger.WoundedConverted);
        Assert.Equal(9_900, turn.Units.Single().Pool.Active);
        Assert.Equal(350, turn.Units.Single().Pool.Wounded);
    }

    [Fact]
    public void 실패하면_병력이_변하지않고_범위안에_멈춰있어도_매_이동턴_판정한다()
    {
        var formation = Formation(1, new HexCoord(2, 0));
        var failed = Orchestrator(new FixedRandom(99)).Run([Unit(default, new HexCoord(4, 0))], maxDays: 1,
            fieldBuildings: [formation], fieldDefinitions: Definitions, fieldDay: 1);
        Assert.False(Assert.Single(failed.FormationTriggers).Activated);
        Assert.Equal(10_000, failed.Units.Single().Pool.Active);

        var stationaryRandom = new FixedRandom(0);
        var stationary = Orchestrator(stationaryRandom).Run([Unit(new HexCoord(1, 0), null)], maxDays: 1,
            fieldBuildings: [formation], fieldDefinitions: Definitions, fieldDay: 1);
        Assert.True(Assert.Single(stationary.FormationTriggers).Activated);
        Assert.Equal(1, stationaryRandom.Calls);
        Assert.Equal(9_900, stationary.Units.Single().Pool.Active);
    }

    [Fact]
    public void 같은_이동에서_진법이_겹쳐도_부대당_한번만_판정한다()
    {
        var random = new FixedRandom(0);
        var first = Formation(1, new HexCoord(2, 0));
        var second = Formation(2, new HexCoord(2, -1));
        var turn = Orchestrator(random).Run([Unit(default, new HexCoord(4, 0))], maxDays: 1,
            fieldBuildings: [first, second], fieldDefinitions: Definitions, fieldDay: 1);

        Assert.Single(turn.FormationTriggers);
        Assert.Equal(1, random.Calls);
        Assert.Equal(9_900, turn.Units.Single().Pool.Active);
    }

    [Fact]
    public void 같은_시드와_입력은_같은_판정결과를_낸다()
    {
        var formation = Formation(1, new HexCoord(2, 0));
        AdvanceTurn Run() => Orchestrator(new SeededRandomSource(731)).Run(
            [Unit(default, new HexCoord(4, 0))], maxDays: 1,
            fieldBuildings: [formation], fieldDefinitions: Definitions, fieldDay: 1);

        Assert.Equal(Run().FormationTriggers, Run().FormationTriggers);
    }
}
