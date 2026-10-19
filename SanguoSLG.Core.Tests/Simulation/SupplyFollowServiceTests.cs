namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public class SupplyFollowServiceTests
{
    private static CombatUnit Unit(int id, int owner, HexCoord position, bool supply = false)
        => new(new FieldUnit(new UnitId(id), new FactionId(owner), position, 2, 2, 1,
                MovementDomain.Land, UnitMode.Standby, null, id),
            new CombatStats(100, 10, 10), new TroopPool(1000, 0), UnitCombatState.Create(50),
            IsSupply: supply, TroopCode: supply ? "supply" : "swordsman");

    [Fact]
    public void 보급부대는_아군_야전부대를_지정하고_매일_위치를_따라간다()
    {
        var supply = Unit(1, 1, new HexCoord(0, 0), true);
        var target = Unit(2, 1, new HexCoord(5, 0));
        var state = new GameState(1, 190, [], [], [], FieldArmies: [supply, target]);
        var assigned = SupplyFollowService.Assign(state, supply.Id, target.Id);
        Assert.True(assigned.Ok, assigned.Error);
        var first = SupplyFollowService.PlanDay(assigned.State.Armies, (_, _) => true, [], []);
        Assert.Equal(new HexCoord(4, 0), first.Single(unit => unit.IsSupply).Field.Target);
        var moved = first.Select(unit => unit.Id == target.Id
            ? unit with { Field = unit.Field with { Position = new HexCoord(0, 2) } } : unit).ToList();
        var second = SupplyFollowService.PlanDay(moved, (_, _) => true, [], []);
        Assert.Null(second.Single(unit => unit.IsSupply).Field.Target);
        Assert.Equal(target.Id, second.Single(unit => unit.IsSupply).Field.FollowUnitId);
    }

    [Fact]
    public void 보급부대는_적군이나_자기자신을_따라갈_수_없다()
    {
        var supply = Unit(1, 1, default, true);
        var enemy = Unit(2, 2, new HexCoord(2, 0));
        var state = new GameState(1, 190, [], [], [], FieldArmies: [supply, enemy]);
        Assert.False(SupplyFollowService.Assign(state, supply.Id, enemy.Id).Ok);
        Assert.False(SupplyFollowService.Assign(state, supply.Id, supply.Id).Ok);
    }

    [Fact]
    public void 목표가_괴멸하면_따라가기를_해제하고_대기한다()
    {
        var supply = Unit(1, 1, default, true) with
        { Field = Unit(1, 1, default, true).Field with { FollowUnitId = new UnitId(2) } };
        var planned = SupplyFollowService.PlanDay([supply], (_, _) => true, [], []);
        Assert.Null(planned[0].Field.FollowUnitId);
        Assert.Equal(UnitMode.Standby, planned[0].Field.Mode);
    }

    [Fact]
    public void 수동_명령과_정지는_따라가기를_해제한다()
    {
        var supply = Unit(1, 1, default, true) with
        { Field = Unit(1, 1, default, true).Field with { FollowUnitId = new UnitId(2) } };
        var state = new GameState(1, 190, [], [], [], FieldArmies: [supply, Unit(2, 1, new HexCoord(2, 0))]);
        var commander = new FieldUnitCommandService((_, _) => true);
        var reassigned = commander.Reassign(state, new FactionId(1),
            new FieldUnitCommandRequest(supply.Id, UnitMode.March, new HexCoord(3, 0)));
        Assert.True(reassigned.Ok, reassigned.Error);
        Assert.Null(reassigned.State.Armies.Single(unit => unit.IsSupply).Field.FollowUnitId);
        var stopped = commander.Stop(state, new FactionId(1), supply.Id);
        Assert.True(stopped.Ok, stopped.Error);
        Assert.Null(stopped.State.Armies.Single(unit => unit.IsSupply).Field.FollowUnitId);
    }

    [Fact]
    public void 따라가기_목표는_저장과_복원_후에도_유지된다()
    {
        var supply = Unit(1, 1, default, true);
        var target = Unit(2, 1, new HexCoord(4, 0));
        var state = new GameState(1, 190, [], [], [], FieldArmies: [supply, target]);
        var assigned = SupplyFollowService.Assign(state, supply.Id, target.Id);
        var json = System.Text.Json.JsonSerializer.Serialize(assigned.State.Armies);
        var restored = System.Text.Json.JsonSerializer.Deserialize<List<CombatUnit>>(json);
        Assert.NotNull(restored);
        Assert.Equal(target.Id, restored.Single(unit => unit.IsSupply).Field.FollowUnitId);
    }

    [Fact]
    public void AI_보급부대는_호위_대상이_사라지면_다른_아군을_따라간다()
    {
        var supply = Unit(1, 2, default, true) with
        { Field = Unit(1, 2, default, true).Field with { FollowUnitId = new UnitId(9) } };
        var replacement = Unit(2, 2, new HexCoord(4, 0));
        var planned = SupplyFollowService.PlanDay([supply, replacement], (_, _) => true, [], [], new FactionId(1));
        Assert.Equal(replacement.Id, planned.Single(unit => unit.IsSupply).Field.FollowUnitId);
    }
}
