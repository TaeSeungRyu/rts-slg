namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using SanguoSLG.Core.Simulation.RenewalMovement;
using Xunit;

public class SummonServiceTests
{
    private static readonly SummonService Service = new(new HexMap(-4, 4, -4, 4),
        new EventTreasureLoader().LoadFromDirectory(TestData.DataDirectory()),
        new SummonedUnitLoader().LoadFromDirectory(TestData.DataDirectory()));

    [Theory]
    [InlineData("tiger_cub", "great_tiger", 10000)]
    [InlineData("dragon_egg", "dragon", 20000)]
    [InlineData("phoenix_egg", "phoenix", 15000)]
    public void 소환수의_증가한_체력이_실제_병력과_최대치에_반영되고_저장된다(
        string item, string code, int health)
    {
        var caller = new CombatUnit(new FieldUnit(new(1), new(1), new(0, 0), 2, 2, 1,
            MovementDomain.Land, UnitMode.Standby, null, 1), new(1000, 10, 10),
            new(1000, 0), UnitCombatState.Create(0), VanguardId: new(1));
        var state = new GameState(10, 190, [], [], [], FieldArmies: [caller],
            FactionTreasures: [new("test-egg", item, item, new(1), 9, "hunt", EquippedBy: new(1))]);
        var result = Service.Order(state, caller.Id, "test-egg", new(1, 0));
        Assert.True(result.Ok, result.Error);
        var unit = Assert.Single(SaveService.Deserialize(SaveService.Serialize(result.State)).Armies,
            unit => unit.IsSummon);
        Assert.Equal(code, unit.TroopCode);
        Assert.Equal(health, unit.Pool.Active);
        Assert.Equal(health, unit.MaxTroops);
        Assert.Equal(1, unit.Field.AttackRange);
        Assert.Equal(1, unit.Field.RangeCastle);
        Assert.Equal(new SummonedUnitLoader().LoadFromDirectory(TestData.DataDirectory())
            .Single(definition => definition.Code == code).Range, unit.Field.RangeCastle);
        Assert.Equal(0, unit.Pool.Wounded);
    }

    [Fact]
    public void 장착한_장수가_소환하면_즉시_출현하고_21일_종료_후_60일_대기한다()
    {
        var field = new FieldUnit(new(1), new(1), new HexCoord(0, 0), 2, 2, 1,
            MovementDomain.Land, UnitMode.Standby, null, 1);
        var caller = new CombatUnit(field, new CombatStats(1000, 10, 10, 1),
            new TroopPool(1000, 0), UnitCombatState.Create(0), VanguardId: new(1));
        var state = new GameState(10, 190, [], [], [], FieldArmies: [caller],
            FactionTreasures: [new("tiger:1", "tiger_cub", "대호의 새끼", new(1), 9,
                "hunt", EquippedBy: new(1))]);
        var ordered = Service.Order(state, caller.Id, "tiger:1", new HexCoord(1, 0));
        Assert.True(ordered.Ok, ordered.Error);
        Assert.False(Service.Order(ordered.State, caller.Id, "tiger:1", new HexCoord(0, 1)).Ok);
        Assert.Equal(new HexCoord(1, 0), Assert.Single(ordered.State.Armies, x => x.IsSummon).Field.Position);
        Assert.Empty(ordered.State.SummonOrders);
        var sameDay = Service.AdvanceDay(ordered.State, 10, out var none);
        Assert.Empty(none);
        Assert.True(sameDay.Armies.Single(x => x.IsSummon).CanInitiateCombat);
        Assert.True(sameDay.Armies.Single(x => x.Id == caller.Id).CanInitiateCombat);
        var nextDay = Service.AdvanceDay(sameDay, 11, out _);
        Assert.True(nextDay.Armies.Single(x => x.Id == caller.Id).CanInitiateCombat);
        Assert.True(nextDay.Armies.Single(x => x.IsSummon).CanInitiateCombat);
        var restored = SaveService.Deserialize(SaveService.Serialize(nextDay));
        Assert.Equal(91, restored.Treasures[0].ReusableDay);
        Assert.False(Service.Order(restored, caller.Id, "tiger:1", new HexCoord(0, 1)).Ok);
        var expired = Service.AdvanceDay(restored, 31, out _);
        Assert.Empty(expired.Summons);
        Assert.Single(expired.Armies);
        Assert.False(Service.Order(expired with { Day = 90 }, caller.Id, "tiger:1", new HexCoord(1, 0)).Ok);
        Assert.True(Service.Order(expired with { Day = 91 }, caller.Id, "tiger:1", new HexCoord(1, 0)).Ok);
    }

    [Fact]
    public void AI도_장착한_알로_적을_발견하면_같은_명령_검사를_거쳐_소환한다()
    {
        var ai = new CombatUnit(new FieldUnit(new(2), new(2), new HexCoord(0, 0), 2, 3, 1,
                MovementDomain.Land, UnitMode.Standby, null, 2),
            new CombatStats(1000, 10, 10), new TroopPool(1000, 0), UnitCombatState.Create(0),
            VanguardId: new(2));
        var enemy = ai with { Field = ai.Field with
            { Id = new(3), Owner = new(1), Position = new HexCoord(2, 0) }, VanguardId = new(3) };
        var state = new GameState(10, 190, [], [], [], FieldArmies: [ai, enemy],
            FactionTreasures: [new("egg", "dragon_egg", "용의 알", new(2), 9, "hunt", EquippedBy: new(2))]);
        var planned = Service.PlanAiOrders(state, 10, new(1));
        Assert.Empty(planned.SummonOrders);
        Assert.Equal(new HexCoord(1, 0), Assert.Single(planned.Armies, x => x.IsSummon).Field.Position);
    }

    [Fact]
    public void 소환수에_공격_목표를_지정하면_연속이동으로_접근한다()
    {
        var map = new HexMap(-4, 8, -4, 4);
        var caller = new CombatUnit(new FieldUnit(new(1), new(1), new(0, 0), 2, 3, 1,
                MovementDomain.Land, UnitMode.Standby, null, 1),
            new CombatStats(1000, 10, 10), new TroopPool(1000, 0), UnitCombatState.Create(0),
            VanguardId: new(1), RenewalPosition: RenewalHexSpace.Center(new(0, 0)));
        var enemy = caller with
        {
            Field = caller.Field with { Id = new(2), Owner = new(2), Position = new(5, 0) },
            VanguardId = new(2), RenewalPosition = RenewalHexSpace.Center(new(5, 0)),
        };
        var state = new GameState(10, 190, [], [], [], FieldArmies: [caller, enemy],
            FactionTreasures: [new("egg", "tiger_cub", "대호의 새끼", new(1), 9,
                "hunt", EquippedBy: new(1))]);
        var service = new SummonService(map,
            new EventTreasureLoader().LoadFromDirectory(TestData.DataDirectory()),
            new SummonedUnitLoader().LoadFromDirectory(TestData.DataDirectory()));
        var summoned = service.Order(state, caller.Id, "egg", new(1, 0));
        Assert.True(summoned.Ok, summoned.Error);
        var tiger = Assert.Single(summoned.State.Armies, unit => unit.IsSummon);
        var passability = new PassabilityMap(map, [], []);
        var command = new FieldUnitCommandService((domain, hex) => passability.CanEnter(domain, hex))
            .Reassign(summoned.State, new(1), new(tiger.Id, UnitMode.Attack, enemy.Field.Position));
        Assert.True(command.Ok, command.Error);
        var ready = service.AdvanceDay(command.State, 10, out _);
        Assert.True(Assert.Single(ready.Armies, unit => unit.IsSummon).CanInitiateCombat);
        var legacy = new AdvanceOrchestrator(new MovementSimulator(passability),
            new CombatPhaseResolver(new BattleResolver(60), 70));
        var moved = new RenewalCampaignAdvanceRunner(map, legacy).Run(ready.Armies, maxDays: 1);
        Assert.NotEqual(tiger.RenewalPosition, moved.Units.Single(unit => unit.Id == tiger.Id).RenewalPosition);
    }
}
