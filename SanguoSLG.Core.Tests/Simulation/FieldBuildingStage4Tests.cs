namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class FieldBuildingStage4Tests
{
    private static readonly FactionId Player = new(1);
    private static readonly FactionId Enemy = new(2);
    private static readonly IReadOnlyList<FieldBuildingDefinition> Definitions =
        new FieldBuildingLoader().LoadFromDirectory(TestData.DataDirectory());

    private static FieldBuilding Building(string code, FactionId owner, HexCoord at, int day = 1)
    {
        var definition = Definitions.First(x => x.Code == code);
        return new(new FieldBuildingId(1), code, owner, at, definition.MaxHitPoints,
            day - definition.BuildDays, day);
    }

    private static FieldUnit Field(int id, FactionId owner, HexCoord at, HexCoord? target,
        UnitMode mode = UnitMode.March, int speed = 3)
        => new(new UnitId(id), owner, at, speed, 3, 1, MovementDomain.Land, mode, target, id, RangeCastle: 1);

    private static CombatUnit Combat(int id, FactionId owner, HexCoord at, HexCoord target)
        => new(Field(id, owner, at, target, UnitMode.Attack), new CombatStats(10_000, 20, 12),
            new TroopPool(10_000, 0), UnitCombatState.Create(60), MaxTroops: 10_000);

    [Fact]
    public void 아군은_건축물_타일을_통과하고_적은_진입하지_못한다()
    {
        var sim = new MovementSimulator(new PassabilityMap(new HexMap(0, 8, -3, 3), [], []));
        var at = new HexCoord(2, 0);
        var building = Building("watchtower", Player, at);
        var ally = sim.Advance([Field(1, Player, new HexCoord(0, 0), at)], 1,
            fieldBuildings: [building], fieldDefinitions: Definitions, fieldDay: 1);
        Assert.Equal(at, ally.Units.Single().Position);

        var enemy = sim.Advance([Field(2, Enemy, new HexCoord(0, 0), at, UnitMode.Attack)], 1,
            fieldBuildings: [building], fieldDefinitions: Definitions, fieldDay: 1);
        Assert.Equal(1, enemy.Units.Single().Position.Distance(at));
    }

    [Fact]
    public void 목책_이동력감소는_범위안에서_한번만_중첩없이_적용된다()
    {
        var sim = new MovementSimulator(new PassabilityMap(new HexMap(0, 8, -3, 3), [], []));
        var palisade = Building("palisade", Player, new HexCoord(2, 0));
        var second = palisade with { Id = new FieldBuildingId(2), Position = new HexCoord(2, -1) };
        var result = sim.Advance([Field(1, Player, new HexCoord(0, 0), new HexCoord(4, 0))], 1,
            fieldBuildings: [palisade, second], fieldDefinitions: Definitions, fieldDay: 1);
        Assert.Equal(new HexCoord(2, 0), result.Units.Single().Position);
    }

    [Fact]
    public void 감시탑_범위안_부대는_시야가_한칸_늘고_중첩되지_않는다()
    {
        var troops = new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
        var vision = new BattlefieldVision(new BalanceConfig(0), troops, Definitions);
        var unit = Combat(1, Player, default, new HexCoord(8, 0)) with { TroopCode = "swordsman" };
        var tower = Building("watchtower", Player, default);
        var second = tower with { Id = new FieldBuildingId(2), Position = new HexCoord(1, 0) };
        var state = new GameState(1, 190, [], [], [], FieldArmies: [unit], FieldBuildings: [tower, second]);
        var visible = vision.VisibleTiles(state, Player, new HexMap(-8, 8, -8, 8));
        Assert.Contains(new HexCoord(4, 0), visible);
        Assert.DoesNotContain(new HexCoord(5, 0), visible);
    }

    [Fact]
    public void 적_건축물은_공격받아_파괴되고_반격하지_않는다()
    {
        var target = new HexCoord(2, 0);
        var building = Building("palisade", Enemy, target);
        var attacker = Combat(1, Player, new HexCoord(1, 0), target);
        var state = new GameState(1, 190, [], [], [], FieldArmies: [attacker], FieldBuildings: [building]);
        var service = new FieldBuildingCombat(new BattleResolver(60), Definitions);
        var result = service.Resolve(state, [attacker]);
        var exchange = Assert.Single(result.Exchanges);
        Assert.True(exchange.Damage > 0);
        Assert.Equal(attacker.Pool, result.Armies.Single().Pool);
        Assert.True(exchange.Destroyed || result.State.Buildings.Single().HitPoints < building.HitPoints);
    }

    [Fact]
    public void 철거는_즉시_제거하고_건축중_부대를_해제한다()
    {
        var unit = Combat(1, Player, default, new HexCoord(4, 0)) with { IsConstructing = true };
        var building = Building("palisade", Player, default) with { BuilderUnit = unit.Id, CompletionDay = 8 };
        var state = new GameState(1, 190, [], [], [], FieldArmies: [unit], FieldBuildings: [building]);
        var service = new FieldConstructionService(Definitions, _ => TerrainType.Plains);
        var result = service.Demolish(state, Player, building.Id);
        Assert.True(result.Ok);
        Assert.Empty(result.State.Buildings);
        Assert.False(result.State.Armies.Single().IsConstructing);
    }
}
