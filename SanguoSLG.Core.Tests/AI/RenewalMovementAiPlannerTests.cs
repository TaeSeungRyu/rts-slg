namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalMovementAiPlannerTests
{
    [Fact]
    public void Plan_UsesSameAttackCommandContractAsPlayer()
    {
        var friendly = RenewalUnitState.Create(new UnitId(1), new(0, 0), new(0, 0), 3)
            with { Owner = new FactionId(1), Mode = RenewalOrderMode.Standby };
        var hostile = RenewalUnitState.Create(new UnitId(2), new(600, 0), new(600, 0), 3)
            with { Owner = new FactionId(2) };
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.Movement, 0,
            [friendly, hostile]);
        var commands = new RenewalCommandService();

        var ai = new RenewalMovementAiPlanner(commands).Plan(state, new FactionId(1));
        var player = commands.Apply(state, new RenewalUnitCommand(1, friendly.Id,
            RenewalOrderMode.Attack, hostile.Position,
            AssignedTarget: RenewalTargetId.ForUnit(hostile.Id)));

        Assert.Equal(player.Units[0], ai.Units[0]);
    }

    [Fact]
    public void Plan_IsDeterministicAndChoosesNearestVisibleHostile()
    {
        var friendly = RenewalUnitState.Create(new UnitId(1), new(0, 0), new(0, 0), 3)
            with { Owner = new FactionId(1), Mode = RenewalOrderMode.Standby };
        var hidden = RenewalUnitState.Create(new UnitId(2), new(100, 0), new(100, 0), 3)
            with { Owner = new FactionId(2), IsVisible = false };
        var near = RenewalUnitState.Create(new UnitId(3), new(400, 0), new(400, 0), 3)
            with { Owner = new FactionId(2) };
        var far = RenewalUnitState.Create(new UnitId(4), new(900, 0), new(900, 0), 3)
            with { Owner = new FactionId(2) };
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.Movement, 0,
            [friendly, hidden, far, near]);
        var planner = new RenewalMovementAiPlanner();

        var first = planner.Plan(state, new FactionId(1));
        var second = planner.Plan(state, new FactionId(1));

        Assert.Equal(RenewalTargetId.ForUnit(near.Id), first.Units[0].AssignedTarget);
        Assert.Equal(first.Units[0], second.Units[0]);
        Assert.Equal(RenewalOrderMode.March, first.Units[1].Mode);
    }

    [Fact]
    public void Plan_DoesNotCommandGarrisonOrProductionUnit()
    {
        var site = new RenewalTargetId(RenewalTargetKind.Site, 9);
        var garrison = RenewalUnitState.Create(new UnitId(1), new(0, 0), new(0, 0), 3)
            with { Owner = new FactionId(1), GarrisonStructure = site };
        var production = RenewalUnitState.Create(new UnitId(2), new(10, 0), new(10, 0), 3)
            with { Owner = new FactionId(1), ProductionOperationId = 5 };
        var enemy = RenewalUnitState.Create(new UnitId(3), new(500, 0), new(500, 0), 3)
            with { Owner = new FactionId(2) };
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.Movement, 0,
            [garrison, production, enemy]);

        var result = new RenewalMovementAiPlanner().Plan(state, new FactionId(1));

        Assert.Equal(state.Units[0], result.Units[0]);
        Assert.Equal(state.Units[1], result.Units[1]);
    }
}
