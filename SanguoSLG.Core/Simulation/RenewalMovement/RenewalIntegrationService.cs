namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

public sealed class RenewalIntegrationService(IRandomSource? random = null)
{
    private readonly IRandomSource _random = random ?? new SeededRandomSource(0);

    public RenewalStepResult ResolveMovementAftermath(RenewalAdvanceState state)
    {
        if (state.Phase != RenewalAdvancePhase.MovementAftermath)
        {
            return new RenewalStepResult(state, []);
        }
        var structures = (state.Structures ?? []).ToList();
        var profiles = state.CombatProfiles?.ToDictionary(x => x.Key, x => x.Value);
        var events = new List<RenewalAdvanceEvent>();
        foreach (var formation in structures.Where(x => x.IsActive
                     && x.Kind == FieldBuildingKind.Formation).OrderBy(x => x.Id.Value))
        {
            foreach (var unit in state.Units.Where(x => x.IsActive && x.Owner != formation.Owner)
                         .OrderBy(x => x.Id.Value))
            {
                if (RenewalHexSpace.NearestHex(formation.Position)
                        .Distance(RenewalHexSpace.NearestHex(unit.Position)) > formation.EffectRadius
                    || _random.Next(0, 100) >= 30 || profiles is null
                    || !profiles.TryGetValue(unit.Id, out var profile))
                {
                    continue;
                }
                var pool = profile.Participant.Pool;
                var wounded = Math.Max(1, pool.Active / 100);
                profiles[unit.Id] = profile with
                {
                    Participant = profile.Participant with
                    {
                        Pool = pool with { Active = pool.Active - wounded,
                            Wounded = pool.Wounded + wounded },
                    },
                };
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.FormationTriggered,
                    state.Day, state.Phase, state.MovementTick, unit.Id,
                    formation.Position, unit.Position, Target: formation.Id, Amount: wounded));
            }
        }

        var removedScouts = structures.Where(x => x.Kind == FieldBuildingKind.ScoutPost
            && state.Units.Any(unit => unit.IsActive && unit.Owner != x.Owner
                && RenewalHexSpace.NearestHex(unit.Position)
                    == RenewalHexSpace.NearestHex(x.Position))).Select(x => x.Id).ToHashSet();
        foreach (var id in removedScouts.OrderBy(x => x.Value))
        {
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.ScoutPostRemoved,
                state.Day, state.Phase, state.MovementTick, Target: id));
        }
        return new RenewalStepResult(state with
        {
            CombatProfiles = profiles,
            Structures = structures.Where(x => !removedScouts.Contains(x.Id)).ToList(),
        }, events);
    }

    public RenewalStepResult ResolveAttackAftermath(RenewalAdvanceState state)
    {
        if (state.Phase != RenewalAdvancePhase.AttackAftermath)
        {
            return new RenewalStepResult(state, []);
        }
        var structures = (state.Structures ?? []).ToDictionary(x => x.Id);
        var events = new List<RenewalAdvanceEvent>();
        var units = state.Units.Select(unit =>
        {
            if (unit.GarrisonStructure is not { } id
                || !structures.TryGetValue(id, out var structure) || structure.IsActive)
            {
                return unit;
            }
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.GarrisonReleased,
                state.Day, state.Phase, state.MovementTick, unit.Id,
                structure.Position, structure.Position, Target: id));
            return unit with { IsActive = true, Position = structure.Position,
                Destination = structure.Position, GarrisonStructure = null };
        }).ToList();
        return new RenewalStepResult(state with { Units = units }, events);
    }

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
        var completedProductionUnits = new HashSet<UnitId>();
        var productions = integration.Productions.Select(operation =>
        {
            if (operation.Completed || operation.CompletionDay > calendarDay)
            {
                return operation;
            }
            completedProductionUnits.Add(operation.Unit);
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.ProductionCompleted,
                state.Day, state.Phase, state.MovementTick, operation.Unit,
                Amount: checked((int)operation.Id),
                Detail: $"gold={operation.RewardGold};provisions={operation.RewardProvisions}"));
            return operation with { Completed = true };
        }).ToList();
        var structures = (state.Structures ?? []).Where(structure =>
        {
            var expired = structure.Kind == FieldBuildingKind.ScoutPost
                && structure.ExpiresDay is { } expires && calendarDay >= expires;
            if (expired)
            {
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.ScoutPostRemoved,
                    state.Day, state.Phase, state.MovementTick, Target: structure.Id));
            }
            return !expired;
        }).ToList();
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

        var units = state.Units.Select(unit => completedProductionUnits.Contains(unit.Id)
            ? unit with { IsActive = true, ProductionOperationId = null }
            : unit).ToList();
        return new RenewalStepResult(state with
        {
            Units = units,
            Structures = structures,
            Integration = integration with
            {
                LastSettledDay = calendarDay,
                ScheduledWorks = works,
                UnitLogistics = logistics,
                ProductionOperations = productions,
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
