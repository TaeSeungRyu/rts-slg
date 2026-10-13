namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class SummonServiceTests
{
    private static readonly SummonService Service = new(new HexMap(-4, 4, -4, 4),
        new EventTreasureLoader().LoadFromDirectory(TestData.DataDirectory()),
        new SummonedUnitLoader().LoadFromDirectory(TestData.DataDirectory()));

    [Fact]
    public void 장착한_장수가_소환하면_다음날_출현하고_21일_종료_후_60일_대기한다()
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
        var sameDay = Service.AdvanceDay(ordered.State, 10, out var none);
        Assert.Empty(none);
        var spawned = Service.AdvanceDay(sameDay, 11, out var appeared);
        Assert.Single(appeared);
        Assert.True(appeared[0].IsSummon);
        Assert.Equal(1, appeared[0].Field.Position.Q);
        var restored = SaveService.Deserialize(SaveService.Serialize(spawned));
        Assert.Equal(92, restored.Treasures[0].ReusableDay);
        Assert.False(Service.Order(restored, caller.Id, "tiger:1", new HexCoord(0, 1)).Ok);
        var expired = Service.AdvanceDay(restored, 32, out _);
        Assert.Empty(expired.Summons);
        Assert.Single(expired.Armies);
        Assert.False(Service.Order(expired with { Day = 91 }, caller.Id, "tiger:1", new HexCoord(1, 0)).Ok);
        Assert.True(Service.Order(expired with { Day = 92 }, caller.Id, "tiger:1", new HexCoord(1, 0)).Ok);
    }
}
