namespace SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalIntegrationService
{
    public RenewalStepResult ResolveDay(RenewalAdvanceState state)
    {
        if (state.Phase != RenewalAdvancePhase.DaySettlement || state.Integration is null)
        {
            return new RenewalStepResult(state, []);
        }

        var integration = state.Integration;
        var calendarDay = integration.CalendarDay + state.Day - 1;
        if (calendarDay <= integration.LastSettledDay)
        {
            return new RenewalStepResult(state, []);
        }

        var events = new List<RenewalAdvanceEvent>
        {
            new(RenewalAdvanceEventKind.DailySettlementApplied, state.Day,
                RenewalAdvancePhase.DaySettlement, state.MovementTick, Amount: calendarDay),
        };
        var works = integration.Works.Select(work =>
        {
            if (work.Completed || work.CompletionDay > calendarDay)
            {
                return work;
            }
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.ScheduledWorkCompleted,
                state.Day, RenewalAdvancePhase.DaySettlement, state.MovementTick,
                Amount: checked((int)work.Id), Detail: work.Kind.ToString()));
            return work with { Completed = true };
        }).ToList();
        var weeklyCount = integration.WeeklySettlementCount;
        if (calendarDay % 7 == 0)
        {
            weeklyCount++;
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.WeeklySettlementApplied,
                state.Day, RenewalAdvancePhase.DaySettlement, state.MovementTick,
                Amount: weeklyCount));
        }

        return new RenewalStepResult(state with
        {
            Integration = integration with
            {
                LastSettledDay = calendarDay,
                ScheduledWorks = works,
                WeeklySettlementCount = weeklyCount,
            },
        }, events);
    }
}
