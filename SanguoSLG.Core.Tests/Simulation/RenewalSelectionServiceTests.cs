namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalSelectionServiceTests
{
    private static RenewalUnitState Unit(int id, int faction, long x, long y = 0) =>
        RenewalUnitState.Create(new UnitId(id), new ContinuousPosition(x, y),
            new ContinuousPosition(x, y), 0) with
        {
            Owner = new FactionId(faction),
            StopReason = id == 2 ? RenewalStopReason.EnemyBlocked : RenewalStopReason.None,
        };

    [Fact]
    public void 겹친부대는_아군우선_ID순으로_모두반환한다()
    {
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.Movement, 0,
            [Unit(3, 2, 10), Unit(2, 1, 30), Unit(1, 1, 0), Unit(4, 1, 500)]);

        var selected = new RenewalSelectionService().UnitsAt(state,
            new ContinuousPosition(0, 0), new FactionId(1), 100);

        Assert.Equal([1, 2, 3], selected.Select(x => x.Unit.Value));
        Assert.Equal([true, true, false], selected.Select(x => x.CanCommand));
        Assert.Equal(RenewalStopReason.EnemyBlocked, selected[1].StopReason);
    }

    [Fact]
    public void 선택반경_밖과_비활성_비가시부대는_목록에서제외한다()
    {
        var hidden = Unit(2, 1, 0) with { IsVisible = false };
        var inactive = Unit(3, 1, 0) with { IsActive = false };
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.Movement, 0,
            [Unit(1, 1, 101), hidden, inactive]);

        var selected = new RenewalSelectionService().UnitsAt(state,
            new ContinuousPosition(0, 0), new FactionId(1), 100);

        Assert.Empty(selected);
    }
}
