namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalIntegrationServiceTests
{
    private static RenewalAdvanceState Settlement(int day, RenewalIntegrationState integration) =>
        new(day, RenewalAdvancePhase.DaySettlement, 50, [], Integration: integration);

    [Fact]
    public void 하루정산은_같은날_재호출해도_한번만_적용된다()
    {
        var service = new RenewalIntegrationService();
        var initial = Settlement(1, new RenewalIntegrationState(7, 0));

        var first = service.ResolveDay(initial);
        var second = service.ResolveDay(first.State);

        Assert.Single(first.Events, x => x.Kind == RenewalAdvanceEventKind.DailySettlementApplied);
        Assert.Single(first.Events, x => x.Kind == RenewalAdvanceEventKind.WeeklySettlementApplied);
        Assert.Empty(second.Events);
        Assert.Equal(1, second.State.Integration!.WeeklySettlementCount);
    }

    [Fact]
    public void 내정_연구_함선_정찰은_완료일_경계에서만_완료된다()
    {
        var works = Enum.GetValues<RenewalWorkKind>()
            .Select((kind, index) => new RenewalScheduledWork(index + 1, kind, 9)).ToList();
        var service = new RenewalIntegrationService();
        var before = service.ResolveDay(Settlement(1,
            new RenewalIntegrationState(8, 0, works)));
        var due = service.ResolveDay(Settlement(2, before.State.Integration!));

        Assert.All(before.State.Integration!.Works, x => Assert.False(x.Completed));
        Assert.All(due.State.Integration!.Works, x => Assert.True(x.Completed));
        Assert.Equal(4, due.Events.Count(x =>
            x.Kind == RenewalAdvanceEventKind.ScheduledWorkCompleted));
    }

    [Fact]
    public void 일시정지처럼_단계가_정산이_아니면_날짜와작업을_바꾸지않는다()
    {
        var integration = new RenewalIntegrationState(7, 0,
            [new RenewalScheduledWork(1, RenewalWorkKind.Research, 7)]);
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.Movement, 0, [],
            Integration: integration);

        var result = new RenewalIntegrationService().ResolveDay(state);

        Assert.Same(state, result.State);
        Assert.Empty(result.Events);
    }
}
