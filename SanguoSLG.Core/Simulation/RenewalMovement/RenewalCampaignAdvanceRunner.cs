namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed class RenewalCampaignAdvanceRunner(
    HexMap map,
    IFieldAdvanceRunner legacyRules) : IFieldAdvanceRunner
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
        var externalTargets = BuildTargets(active, castles);
        var renewalUnits = active.Select(unit => ToRenewal(unit, active, castles,
            constructionUnits?.Contains(unit.Id) == true)).ToList();
        var state = simulator.Start(renewalUnits, externalTargets);
        var ticks = new List<MovementTick>();
        var days = Math.Clamp(maxDays, 0, RenewalAdvanceSimulator.DaysPerAdvance);
        for (var day = 0; day < days && !state.IsCompleted; day++)
        {
            while (state.Phase == RenewalAdvancePhase.Movement)
            {
                state = simulator.StepMovementTick(state).State;
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

    private static RenewalUnitState ToRenewal(CombatUnit unit, IReadOnlyList<CombatUnit> units,
        IReadOnlyList<SiegeSite>? castles, bool constructing)
    {
        var position = unit.RenewalPosition ?? RenewalHexSpace.Center(unit.Field.Position);
        var destination = RenewalHexSpace.Center(unit.Field.Target ?? unit.Field.Position);
        var target = AssignedTarget(unit, units, castles);
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
        IReadOnlyList<CombatUnit> units, IReadOnlyList<SiegeSite>? castles)
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
        var site = (castles ?? []).Select((value, index) => (value, index))
            .FirstOrDefault(entry => entry.value.Contains(target));
        return site.value is null ? null : new RenewalTargetId(RenewalTargetKind.Site, site.index + 1);
    }

    private static IReadOnlyList<RenewalTargetState> BuildTargets(IReadOnlyList<CombatUnit> units,
        IReadOnlyList<SiegeSite>? castles)
    {
        var result = units.Select(unit => new RenewalTargetState(RenewalTargetId.ForUnit(unit.Id),
            unit.Field.Owner, unit.RenewalPosition ?? RenewalHexSpace.Center(unit.Field.Position))).ToList();
        result.AddRange((castles ?? []).Select((site, index) => new RenewalTargetState(
            new RenewalTargetId(RenewalTargetKind.Site, index + 1), site.Owner,
            RenewalHexSpace.Center(site.Position))));
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
