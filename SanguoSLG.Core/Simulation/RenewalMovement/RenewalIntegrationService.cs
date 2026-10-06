namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

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
        var logistics = ResolveLogistics(state, integration.Logistics, events);
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
                UnitLogistics = logistics,
                WeeklySettlementCount = weeklyCount,
            },
        }, events);
    }

    private static IReadOnlyDictionary<UnitId, RenewalUnitLogistics> ResolveLogistics(
        RenewalAdvanceState state,
        IReadOnlyDictionary<UnitId, RenewalUnitLogistics> source,
        ICollection<RenewalAdvanceEvent> events)
    {
        var positions = state.Units.Where(x => x.IsActive).ToDictionary(x => x.Id);
        var result = source.ToDictionary(x => x.Key, x => x.Value);
        foreach (var entry in source.Values.Where(x => !x.IsSupply).OrderBy(x => x.Unit.Value))
        {
            if (!positions.TryGetValue(entry.Unit, out var unit))
            {
                continue;
            }
            var consumption = Math.Max(0, entry.DailyConsumption);
            var protectedByFort = (state.Structures ?? []).Any(structure => structure.IsActive
                && structure.Owner == unit.Owner && structure.Kind == FieldBuildingKind.Fort
                && RenewalHexSpace.NearestHex(structure.Position)
                    .Distance(RenewalHexSpace.NearestHex(unit.Position)) <= structure.EffectRadius);
            if (protectedByFort)
            {
                consumption = consumption * 60 / 100;
            }

            var own = Math.Min(entry.Provisions, consumption);
            var updated = entry with { Provisions = entry.Provisions - own };
            var deficit = consumption - own;
            foreach (var supplier in result.Values.Where(x => x.IsSupply && x.SupplyStock > 0)
                         .OrderBy(x => x.Unit.Value).ToList())
            {
                if (deficit <= 0 || !positions.TryGetValue(supplier.Unit, out var supplyUnit)
                    || supplyUnit.Owner != unit.Owner
                    || RenewalHexSpace.NearestHex(supplyUnit.Position)
                        .Distance(RenewalHexSpace.NearestHex(unit.Position)) > supplier.SupplyRadius)
                {
                    continue;
                }
                var amount = Math.Min(deficit, supplier.SupplyStock);
                result[supplier.Unit] = supplier with { SupplyStock = supplier.SupplyStock - amount };
                updated = updated with { Provisions = updated.Provisions + amount };
                deficit -= amount;
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.SupplyTransferred,
                    state.Day, RenewalAdvancePhase.DaySettlement, state.MovementTick,
                    supplier.Unit, supplyUnit.Position, unit.Position,
                    Target: RenewalTargetId.ForUnit(unit.Id), Amount: amount));
            }
            result[entry.Unit] = updated;
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.ProvisionsConsumed,
                state.Day, RenewalAdvancePhase.DaySettlement, state.MovementTick,
                entry.Unit, unit.Position, unit.Position, Amount: consumption));
        }
        return result;
    }
}
