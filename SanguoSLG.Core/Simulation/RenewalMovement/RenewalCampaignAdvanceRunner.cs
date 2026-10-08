namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed class RenewalCampaignAdvanceRunner(
    HexMap map,
    IFieldAdvanceRunner legacyRules,
    Action<RenewalCampaignTraceEntry>? trace = null) : IFieldAdvanceRunner
{
    public bool CanEnter(MovementDomain domain, HexCoord coord) =>
        legacyRules.CanEnter(domain, coord);

    public AdvanceTurn Run(IReadOnlyList<CombatUnit> units, int maxDays = 7,
        IReadOnlyList<SiegeSite>? castles = null, IReadOnlySet<UnitId>? deployedToday = null,
        IReadOnlySet<UnitId>? constructionUnits = null,
        IReadOnlyList<FieldBuilding>? fieldBuildings = null,
        IReadOnlyList<FieldBuildingDefinition>? fieldDefinitions = null, int fieldDay = 0)
    {
        var active = units.Where(unit => unit.Pool.Active > 0).OrderBy(unit => unit.Id.Value).ToList();
        var blocked = (fieldBuildings ?? [])
            .Where(building => !building.IsExpired(fieldDay))
            .Select(building => building.Position)
            .ToHashSet();
        var simulator = new RenewalAdvanceSimulator(new RenewalMovementMap(map, blocked));
        var liveBuildings = (fieldBuildings ?? [])
            .Where(building => !building.IsExpired(fieldDay)).ToList();
        var externalTargets = BuildTargets(castles, liveBuildings);
        var renewalUnits = active.Select(unit => ToRenewal(unit, active, castles, liveBuildings,
            constructionUnits?.Contains(unit.Id) == true)).ToList();
        var state = simulator.Start(renewalUnits, externalTargets);
        Trace(state, trace);
        var ticks = new List<MovementTick>();
        var days = Math.Clamp(maxDays, 0, RenewalAdvanceSimulator.DaysPerAdvance);
        for (var day = 0; day < days && !state.IsCompleted; day++)
        {
            while (state.Phase == RenewalAdvancePhase.Movement)
            {
                state = simulator.StepMovementTick(state).State;
                Trace(state, trace);
                ticks.Add(new MovementTick(day + 1,
                    state.Units.Where(unit => unit.IsActive).Select(ToField).ToList(), []));
            }
            while (!state.IsCompleted && state.Phase != RenewalAdvancePhase.Movement)
            {
                state = simulator.StepPhase(state).State;
            }
        }

        var positions = state.Units.ToDictionary(unit => unit.Id);
        var moved = active.Select(unit => positions.TryGetValue(unit.Id, out var position)
            ? unit with
            {
                Field = unit.Field with { Position = RenewalHexSpace.NearestHex(position.Position) },
                RenewalPosition = position.Position,
            }
            : unit).ToList();
        var movedById = moved.ToDictionary(unit => unit.Id);
        var resolved = legacyRules.Run(moved, 0, castles, deployedToday, constructionUnits,
            fieldBuildings, fieldDefinitions, fieldDay);
        var synchronized = resolved.Units.Select(unit => movedById.TryGetValue(unit.Id, out var before)
                && unit.Field.Position == before.Field.Position
            ? unit
            : unit with { RenewalPosition = RenewalHexSpace.Center(unit.Field.Position) }).ToList();
        var synchronizedEntered = resolved.EnteredCastle.Select(unit => unit with
        {
            RenewalPosition = RenewalHexSpace.Center(unit.Field.Position),
        }).ToList();
        var movement = new AdvanceResult(ticks, synchronized.Select(unit => unit.Field).ToList(),
            StopReason.MaxDays, days, resolved.Movement.EnteredCastle);
        return resolved with { Units = synchronized, Entered = synchronizedEntered, Movement = movement };
    }

    private static void Trace(RenewalAdvanceState state,
        Action<RenewalCampaignTraceEntry>? trace)
    {
        if (trace is null)
        {
            return;
        }
        foreach (var unit in state.Units.OrderBy(unit => unit.Id.Value))
        {
            trace(new RenewalCampaignTraceEntry(state.Day, state.MovementTick, unit.Id,
                unit.Mode, unit.OriginalDestination ?? unit.Destination,
                unit.Destination, unit.Position, unit.AssignedTarget, unit.PursuitTarget,
                unit.StopReason, unit.PathIndex, unit.Path?.Count ?? 0));
        }
    }

    private static RenewalUnitState ToRenewal(CombatUnit unit, IReadOnlyList<CombatUnit> units,
        IReadOnlyList<SiegeSite>? castles, IReadOnlyList<FieldBuilding> buildings,
        bool constructing)
    {
        var position = unit.RenewalPosition ?? RenewalHexSpace.Center(unit.Field.Position);
        var destination = RenewalHexSpace.Center(unit.Field.Target ?? unit.Field.Position);
        var target = AssignedTarget(unit, units, castles, buildings);
        return new RenewalUnitState(unit.Id, position, destination,
            Math.Max(0, unit.Field.Speed), position == destination, unit.Field.Owner,
            unit.Field.Domain,
            Mode: constructing ? RenewalOrderMode.Standby : unit.Field.Mode switch
            {
                UnitMode.March => RenewalOrderMode.March,
                UnitMode.Advance => RenewalOrderMode.Advance,
                UnitMode.Attack => RenewalOrderMode.Attack,
                _ => RenewalOrderMode.Standby,
            },
            OriginalDestination: destination,
            OriginalWaypoints: unit.Field.Waypoints?.Select(RenewalHexSpace.Center).ToList(),
            AssignedTarget: target,
            DetectionRange: unit.Field.Detection,
            AttackRange: unit.Field.AttackRange,
            CommandId: unit.Field.CommandOrder,
            BuildingAttackRange: unit.Field.RangeCastle,
            CastleAttackRange: unit.Field.RangeCastle);
    }

    private static RenewalTargetId? AssignedTarget(CombatUnit source,
        IReadOnlyList<CombatUnit> units, IReadOnlyList<SiegeSite>? castles,
        IReadOnlyList<FieldBuilding> buildings)
    {
        if (source.Field.Mode != UnitMode.Attack || source.Field.Target is not { } target)
        {
            return null;
        }
        var unit = units.FirstOrDefault(candidate => candidate.Field.Owner != source.Field.Owner
            && candidate.Field.Position == target);
        if (unit is not null)
        {
            return RenewalTargetId.ForUnit(unit.Id);
        }
        var building = buildings.FirstOrDefault(candidate => candidate.Owner != source.Field.Owner
            && candidate.Position == target);
        if (building is not null)
        {
            return new RenewalTargetId(RenewalTargetKind.Building, building.Id.Value);
        }
        var site = (castles ?? []).Select((value, index) => (value, index))
            .FirstOrDefault(entry => entry.value.Owner != source.Field.Owner
                && entry.value.Contains(target));
        return site.value is null ? null : new RenewalTargetId(RenewalTargetKind.Site, site.index + 1);
    }

    private static IReadOnlyList<RenewalTargetState> BuildTargets(
        IReadOnlyList<SiegeSite>? castles, IReadOnlyList<FieldBuilding> buildings)
    {
        var result = new List<RenewalTargetState>();
        result.AddRange((castles ?? []).Select((site, index) => new RenewalTargetState(
            new RenewalTargetId(RenewalTargetKind.Site, index + 1), site.Owner,
            RenewalHexSpace.Center(site.Position), Footprint: site.Footprint
                .Select(RenewalHexSpace.Center).ToList())));
        result.AddRange(buildings.Select(building => new RenewalTargetState(
            new RenewalTargetId(RenewalTargetKind.Building, building.Id.Value), building.Owner,
            RenewalHexSpace.Center(building.Position))));
        return result;
    }

    private static FieldUnit ToField(RenewalUnitState unit) => new(unit.Id, unit.Owner,
        RenewalHexSpace.NearestHex(unit.Position), unit.MovementPerDay, unit.DetectionRange,
        unit.AttackRange, unit.Domain, unit.Mode switch
        {
            RenewalOrderMode.March => UnitMode.March,
            RenewalOrderMode.Advance => UnitMode.Advance,
            _ => UnitMode.Attack,
        }, RenewalHexSpace.NearestHex(unit.Destination), checked((int)unit.CommandId),
        unit.CastleAttackRange);
}
