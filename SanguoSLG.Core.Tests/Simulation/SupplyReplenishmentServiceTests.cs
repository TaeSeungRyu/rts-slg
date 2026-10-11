namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public sealed class SupplyReplenishmentServiceTests
{
    private static CombatUnit Unit(int id, int q, bool supply)
    {
        var field = new FieldUnit(new UnitId(id), new FactionId(1), new HexCoord(q, 0),
            2, 2, 1, MovementDomain.Land, UnitMode.Standby, null, id, 1);
        return new CombatUnit(field, new CombatStats(5000, 8, 8), new TroopPool(5000, 0),
            UnitCombatState.Create(60), MaxTroops: 10000, Provisions: supply ? 200 : 20,
            IsSupply: supply, TroopCode: "swordsman", Training: supply ? 80 : 40,
            SupplyCargo: supply ? [new SupplyComponent("swordsman", 5000, 80)] : null,
            CargoGold: supply ? 300 : 0);
    }

    private static GameState State() => new(1, 190, [], [], [],
        FieldArmies: [Unit(1, 0, true), Unit(2, 1, false)]);

    [Fact]
    public void 예약은_즉시_자원을_옮기지_않고_중복_목표를_거부한다()
    {
        var service = new SupplyReplenishmentService();
        var before = State();
        var order = new SupplyReplenishmentOrder(new UnitId(1), new UnitId(2), 1000, 100, 50);
        var queued = service.Queue(before, new FactionId(1), order);

        Assert.True(queued.Ok);
        Assert.Equal(5000, queued.State.Armies[1].Pool.Active);
        Assert.Equal(200, queued.State.Armies[0].Provisions);
        Assert.Single(queued.State.ReplenishmentOrders);
        Assert.False(service.Queue(queued.State, new FactionId(1), order).Ok);
        var loaded = SaveService.Deserialize(SaveService.Serialize(queued.State));
        Assert.Equal(order, Assert.Single(loaded.ReplenishmentOrders));
    }

    [Fact]
    public void 다음진행에_병력_금_군량이_정확히_이전되고_예약이_소비된다()
    {
        var service = new SupplyReplenishmentService();
        var queued = service.Queue(State(), new FactionId(1),
            new SupplyReplenishmentOrder(new UnitId(1), new UnitId(2), 1000, 100, 50));
        var (after, reports) = service.Execute(queued.State);

        Assert.Equal(4000, after.Armies[0].Pool.Active);
        Assert.Equal(6000, after.Armies[1].Pool.Active);
        Assert.Equal(200, after.Armies[0].CargoGold);
        Assert.Equal(100, after.Armies[1].CargoGold);
        Assert.Equal(150, after.Armies[0].Provisions);
        Assert.Equal(70, after.Armies[1].Provisions);
        Assert.Equal(47, after.Armies[1].Training);
        Assert.Empty(after.ReplenishmentOrders);
        Assert.Single(reports);
    }

    [Fact]
    public void 대상이_멀어지면_예약은_실패하고_자원은_유지된다()
    {
        var service = new SupplyReplenishmentService();
        var queued = service.Queue(State(), new FactionId(1),
            new SupplyReplenishmentOrder(new UnitId(1), new UnitId(2), 1000, 100, 50));
        var moved = queued.State with { FieldArmies = queued.State.Armies.Select(unit => unit.Id.Value == 2
            ? unit with { Field = unit.Field with { Position = new HexCoord(3, 0) } } : unit).ToList() };
        var (after, reports) = service.Execute(moved);

        Assert.Equal(5000, after.Armies[0].Pool.Active);
        Assert.Equal(300, after.Armies[0].CargoGold);
        Assert.Empty(after.ReplenishmentOrders);
        Assert.Contains("실패", reports[0]);
    }
}
