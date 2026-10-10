namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed class RenewalCampaignAdvanceRunner(
    HexMap map,
    IFieldAdvanceRunner legacyRules,
    Action<RenewalCampaignTraceEntry>? trace = null) : IFieldAdvanceRunner
{
    public bool UsesContinuousMovement => true;
    private GameState? _campaign;
    public void SetCampaignState(GameState state) => _campaign = state;

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
            .Where(building => !building.IsExpired(fieldDay)
                && (fieldDefinitions is null
                    ? building.DefinitionCode != "scout_post"
                    : fieldDefinitions.Any(definition => definition.Code == building.DefinitionCode
                        && definition.Kind != FieldBuildingKind.ScoutPost)))
            .Select(building => building.Position)
            .ToHashSet();
        blocked.UnionWith((castles ?? []).SelectMany(site => site.Footprint));
        blocked.UnionWith(_campaign?.Ruins.Select(ruin => ruin.Position) ?? []);
        var movementMap = new RenewalMovementMap(map, blocked);
        var simulator = new RenewalAdvanceSimulator(movementMap);
        var liveBuildings = (fieldBuildings ?? [])
            .Where(building => !building.IsExpired(fieldDay)).ToList();
        var attackableCodes = (fieldDefinitions ?? [])
            .Where(definition => definition.CanBeTargeted)
            .Select(definition => definition.Code).ToHashSet(StringComparer.Ordinal);
        // Standalone runner fixtures do not supply the catalogue. In that case keep
        // ordinary structures targetable while retaining the scout-post exception.
        var attackableBuildings = liveBuildings
            .Where(building => fieldDefinitions is null
                ? building.DefinitionCode != "scout_post"
                : attackableCodes.Contains(building.DefinitionCode)).ToList();
        var externalTargets = BuildTargets(castles, attackableBuildings);
        var ruins = (_campaign?.Ruins ?? []).OrderBy(ruin => ruin.Id, StringComparer.Ordinal).ToList();
        foreach (var (ruin, index) in ruins.Select((ruin, index) => (ruin, index)))
        {
            var status = _campaign!.RuinStatus.FirstOrDefault(value => value.RuinId == ruin.Id);
            if (status is null || status.Defenders <= 0 || status.IsProtected(fieldDay)) continue;
            externalTargets.Add(new RenewalTargetState(new(RenewalTargetKind.Building, -index - 1),
                status.Owner ?? new FactionId(-1), RenewalHexSpace.Center(ruin.Position),
                AutoAcquirable: false));
        }
        var renewalUnits = active.Select(unit =>
        {
            var renewal = ToRenewal(unit, active, castles, attackableBuildings,
                constructionUnits?.Contains(unit.Id) == true
                    || unit.State.Statuses.Any(status => status.IsDaze && !status.IsExpired)
                    || liveBuildings.Any(building => building.GarrisonUnit == unit.Id));
            // A stale intermediate waypoint can be individually passable yet
            // unreachable from the current position (e.g. a city's wall). Do
            // not strand the unit for every subsequent day: continue toward
            // the original destination through the reachable waypoints.
            if (renewal.OriginalWaypoints is { Count: > 0 } originalWaypoints)
            {
                var reachable = new List<ContinuousPosition>();
                var cursor = renewal.Position;
                foreach (var waypoint in originalWaypoints)
                {
                    if (movementMap.FindPath(unit.Field.Domain, cursor, waypoint).Count == 0) continue;
                    reachable.Add(waypoint);
                    cursor = waypoint;
                }
                if (reachable.Count != originalWaypoints.Count)
                    renewal = renewal with { OriginalWaypoints = reachable,
                        Destination = reachable.Count > 0 ? reachable[0] : renewal.OriginalDestination!.Value };
            }
            var ruinIndex = ruins.FindIndex(ruin => ruin.Position == unit.Field.Target);
            if (unit.Field.Mode == UnitMode.Attack && ruinIndex >= 0)
                renewal = renewal with { AssignedTarget = new(RenewalTargetKind.Building, -ruinIndex - 1) };
            if (renewal.OriginalWaypoints is not { Count: > 0 }
                && blocked.Contains(RenewalHexSpace.NearestHex(renewal.Destination))
                && OwnDestination(unit, castles) is null)
            {
                var approach = RenewalHexSpace.NearestHex(renewal.Destination).Neighbors()
                    .Select(RenewalHexSpace.Center)
                    .Where(candidate => movementMap.CanStand(unit.Field.Domain, candidate,
                        RenewalAdvanceSimulator.UnitCollisionRadius))
                    .Where(candidate => movementMap.FindPath(unit.Field.Domain,
                        renewal.Position, candidate).Count > 0)
                    .OrderBy(candidate => candidate.DistanceSquaredTo(renewal.Position))
                    .ThenBy(candidate => candidate.X).ThenBy(candidate => candidate.Y)
                    .FirstOrDefault();
                if (approach != default)
                    renewal = renewal with { Destination = approach, Arrived = false };
            }
            if (OwnDestination(unit, castles) is { } ownSite
                && renewal.OriginalWaypoints is not { Count: > 0 })
            {
                var approach = ownSite.Footprint.SelectMany(tile => tile.Neighbors()).Distinct()
                    .Where(tile => movementMap.CanStand(unit.Field.Domain, RenewalHexSpace.Center(tile),
                        RenewalAdvanceSimulator.UnitCollisionRadius))
                    .Where(tile => movementMap.FindPath(unit.Field.Domain, renewal.Position,
                        RenewalHexSpace.Center(tile)).Count > 0)
                    .OrderBy(tile => renewal.Position.DistanceSquaredTo(RenewalHexSpace.Center(tile)))
                    .ThenBy(tile => tile.Q).ThenBy(tile => tile.R).Cast<HexCoord?>().FirstOrDefault();
                if (approach is { } entry)
                    renewal = renewal with { Destination = RenewalHexSpace.Center(entry),
                        OriginalDestination = RenewalHexSpace.Center(entry), Arrived = false };
            }
            return renewal;
        }).ToList();
        var state = simulator.Start(renewalUnits, externalTargets);
        Trace(state, trace);
        var ticks = new List<MovementTick>();
        var days = Math.Clamp(maxDays, 0, RenewalAdvanceSimulator.DaysPerAdvance);
        for (var day = 0; day < days && !state.IsCompleted; day++)
        {
            while (state.Phase == RenewalAdvancePhase.Movement)
            {
                state = simulator.StepMovementTick(state).State;
                var enteredNow = state.Units.Where(position => position.IsActive
                    && active.First(unit => unit.Id == position.Id) is { } source
                    && source.Field.Speed > 0 && constructionUnits?.Contains(source.Id) != true
                    && !source.State.Statuses.Any(status => status.IsDaze && !status.IsExpired)
                    && position.OriginalWaypointIndex >= (position.OriginalWaypoints?.Count ?? 0)
                    && OwnDestination(source, castles) is { } ownSite
                    && ownSite.Footprint.Any(tile => position.Position.DistanceTo(RenewalHexSpace.Center(tile))
                        <= ContinuousPosition.UnitsPerTile)).ToList();
                var enteringIds = enteredNow.Select(position => position.Id).ToHashSet();
                state = state with { Units = state.Units.Select(position => enteringIds.Contains(position.Id)
                    ? position with { IsActive = false } : position).ToList() };
                Trace(state, trace);
                var activeUnits = state.Units.Where(unit => unit.IsActive).ToList();
                ticks.Add(new MovementTick(day + 1,
                    activeUnits.Select(ToField).ToList(), enteredNow.Select(position =>
                        new TickEvent(TickEventKind.EnteredCastle, position.Id, null)).ToList())
                {
                    ContinuousTick = state.MovementTick,
                    EnteredUnits = enteredNow.Select(ToField).ToList(),
                    ContinuousPositions = state.Units.ToDictionary(unit => unit.Id,
                        unit => unit.Position),
                });
            }
            while (!state.IsCompleted && state.Phase != RenewalAdvancePhase.Movement)
            {
                state = simulator.StepPhase(state).State;
            }
        }

        var positions = state.Units.ToDictionary(unit => unit.Id);
        var originals = active.ToDictionary(unit => unit.Id);
        var moved = active.Select(unit => positions.TryGetValue(unit.Id, out var position)
            ? unit with
            {
                Field = unit.Field with
                {
                    Position = RenewalHexSpace.NearestHex(position.Position),
                    Waypoints = position.OriginalWaypoints?.Skip(position.OriginalWaypointIndex)
                        .Select(RenewalHexSpace.NearestHex).ToList(),
                    ContinuousWaypoints = position.OriginalWaypoints?.Skip(position.OriginalWaypointIndex).ToList(),
                    PursuitTarget = position.PursuitTarget,
                    AssignedUnitTarget = position.AssignedTarget is { Kind: RenewalTargetKind.Unit } assigned
                        ? new UnitId(checked((int)assigned.Value)) : unit.Field.AssignedUnitTarget,
                    Mode = position.Mode == RenewalOrderMode.Standby
                        && constructionUnits?.Contains(unit.Id) != true
                        && !unit.State.Statuses.Any(status => status.IsDaze && !status.IsExpired)
                        && !liveBuildings.Any(building => building.GarrisonUnit == unit.Id)
                        ? UnitMode.Standby : unit.Field.Mode,
                },
                RenewalPosition = position.Position,
            }
            : unit).ToList();
        var temporaryBlockers = new HashSet<UnitId>();
        var movementOrders = moved.ToDictionary(unit => unit.Id, unit => unit.Field);
        var blockerTargets = new Dictionary<UnitId, UnitId>();
        foreach (var unit in moved)
        {
            if (!unit.CanInitiateCombat || unit.Pool.Active <= 0 || constructionUnits?.Contains(unit.Id) == true
                || unit.State.Statuses.Any(status => status.IsDaze && !status.IsExpired)
                || liveBuildings.Any(building => building.GarrisonUnit == unit.Id)
                || !positions.TryGetValue(unit.Id, out var stoppedUnit) || !stoppedUnit.IsActive
                || stoppedUnit.StopReason != RenewalStopReason.EnemyBlocked) continue;
            var radius = (long)unit.Field.AttackRange * ContinuousPosition.UnitsPerTile;
            var blocker = moved.Where(candidate => candidate.Field.Owner != unit.Field.Owner
                    && candidate.Id == stoppedUnit.BlockingUnit
                    && candidate.Pool.Active > 0 && positions[candidate.Id].IsActive
                    && stoppedUnit.Position.DistanceSquaredTo(positions[candidate.Id].Position) <= radius * radius)
                .OrderBy(candidate => stoppedUnit.Position.DistanceSquaredTo(positions[candidate.Id].Position))
                .ThenBy(candidate => candidate.Id.Value).FirstOrDefault();
            if (blocker is null) continue;
            blockerTargets.TryAdd(unit.Id, blocker.Id);
            // 통행을 막는 상대도 같은 교환에 대응한다. 원래 명령은 정산 후 복원한다.
            if (blocker.CanInitiateCombat && constructionUnits?.Contains(blocker.Id) != true
                && !blocker.State.Statuses.Any(status => status.IsDaze && !status.IsExpired)
                && !liveBuildings.Any(building => building.GarrisonUnit == blocker.Id))
                blockerTargets.TryAdd(blocker.Id, unit.Id);
        }
        moved = moved.Select(unit =>
        {
            if (!blockerTargets.TryGetValue(unit.Id, out var blocker)) return unit;
            temporaryBlockers.Add(unit.Id);
            return unit with
            {
                Field = unit.Field with { Mode = UnitMode.Attack, AssignedUnitTarget = blocker },
            };
        }).ToList();
        // 지정 대상과 싸울 수 없는 부대가 실제 공격을 받으면 공격한 부대에 대응한다.
        // 임시 목표는 이번 공격턴에만 유지해 성/건물과 야전 부대를 동시에 치지 않게 한다.
        var intentions = CombatPhase.DetectEngagements(moved);
        var alreadyAttacking = intentions.Select(intent => intent.Attacker).ToHashSet();
        var incoming = intentions.SelectMany(intent => intent.Targets.Select(target =>
                (Target: target, Attacker: intent.Attacker)))
            .GroupBy(hit => hit.Target).ToDictionary(group => group.Key, group => group
                .Select(hit => hit.Attacker).ToHashSet());
        moved = moved.Select(unit =>
        {
            if (unit.Field.Mode != UnitMode.Attack || alreadyAttacking.Contains(unit.Id)
                || !unit.CanInitiateCombat || unit.Pool.Active <= 0
                || constructionUnits?.Contains(unit.Id) == true
                || unit.State.Statuses.Any(status => status.IsDaze && !status.IsExpired)
                || liveBuildings.Any(building => building.GarrisonUnit == unit.Id)
                || !incoming.TryGetValue(unit.Id, out var attackers)) return unit;
            var response = moved.Where(candidate => attackers.Contains(candidate.Id)
                    && candidate.Pool.Active > 0 && positions[candidate.Id].IsActive
                    && positions[unit.Id].Position.DistanceTo(positions[candidate.Id].Position)
                        <= (long)unit.Field.AttackRange * ContinuousPosition.UnitsPerTile)
                .OrderBy(candidate => positions[unit.Id].Position.DistanceSquaredTo(positions[candidate.Id].Position))
                .ThenBy(candidate => candidate.Field.CommandOrder).ThenBy(candidate => candidate.Id.Value)
                .FirstOrDefault();
            if (response is null) return unit;
            temporaryBlockers.Add(unit.Id);
            return unit with { Field = unit.Field with { AssignedUnitTarget = response.Id } };
        }).ToList();
        var movedById = moved.ToDictionary(unit => unit.Id);
        var entered = moved.Where(unit => !positions[unit.Id].IsActive)
            .Select(unit => unit with { State = unit.State.ReturnToCastle() }).ToList();
        var enteredIds = entered.Select(unit => unit.Id).ToHashSet();
        var resolved = legacyRules.Run(moved.Where(unit => !enteredIds.Contains(unit.Id)).ToList(),
            0, castles, deployedToday, constructionUnits,
            fieldBuildings, fieldDefinitions, fieldDay);
        var synchronized = resolved.Units.Select(unit =>
        {
            if (!movedById.TryGetValue(unit.Id, out var before)) return unit;
            var samePosition = unit.Field.Position == before.Field.Position;
            return unit with
            {
                Field = before.Field with
                {
                    Position = unit.Field.Position,
                    Mode = temporaryBlockers.Contains(unit.Id) ? movementOrders[unit.Id].Mode : before.Field.Mode,
                    AssignedUnitTarget = temporaryBlockers.Contains(unit.Id)
                        ? originals[unit.Id].Field.AssignedUnitTarget
                        : before.Field.AssignedUnitTarget,
                },
                RenewalPosition = samePosition ? before.RenewalPosition
                    : RenewalHexSpace.Center(unit.Field.Position),
            };
        }).ToList();
        var synchronizedEntered = entered.Concat(resolved.EnteredCastle.Select(unit => unit with
        {
            RenewalPosition = RenewalHexSpace.Center(unit.Field.Position),
        })).ToList();
        var movement = new AdvanceResult(ticks, synchronized.Select(unit => unit.Field).ToList(),
            StopReason.MaxDays, days, synchronizedEntered.Select(unit => unit.Id).ToList());
        return resolved with { Units = synchronized, Entered = synchronizedEntered, Movement = movement };
    }

    private static SiegeSite? OwnDestination(CombatUnit unit, IReadOnlyList<SiegeSite>? castles) =>
        unit.Field.Target is { } target ? (castles ?? []).FirstOrDefault(site =>
            site.Owner == unit.Field.Owner && site.Contains(target)
            && (unit.Field.ReturnCity is null || site.City == unit.Field.ReturnCity)) : null;

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
        var destination = unit.Field.ContinuousTarget
            ?? RenewalHexSpace.Center(unit.Field.Target ?? unit.Field.Position);
        var waypoints = unit.Field.ContinuousWaypoints
            ?? unit.Field.Waypoints?.Select(RenewalHexSpace.Center).ToList();
        var firstDestination = waypoints is { Count: > 0 } ? waypoints[0] : destination;
        var target = AssignedTarget(unit, units, castles, buildings);
        return new RenewalUnitState(unit.Id, position, firstDestination,
            Math.Max(0, unit.Field.Speed), position == firstDestination && firstDestination == destination,
            unit.Field.Owner,
            unit.Field.Domain,
            Mode: constructing ? RenewalOrderMode.Standby : unit.Field.Mode switch
            {
                UnitMode.March => RenewalOrderMode.March,
                UnitMode.Advance => RenewalOrderMode.Advance,
                UnitMode.Attack => RenewalOrderMode.Attack,
                _ => RenewalOrderMode.Standby,
            },
            OriginalDestination: destination,
            OriginalWaypoints: waypoints,
            AssignedTarget: target,
            PursuitTarget: unit.Field.PursuitTarget,
            LastKnownTargetPosition: target is null ? null : destination,
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
        if (source.Field.AssignedUnitTarget is { } assignedId)
            return RenewalTargetId.ForUnit(assignedId);
        var unit = units.FirstOrDefault(candidate => candidate.Field.Owner != source.Field.Owner
            && (source.Field.AssignedUnitTarget is { } id ? candidate.Id == id : candidate.Field.Position == target));
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

    private static List<RenewalTargetState> BuildTargets(
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
            RenewalOrderMode.Attack => UnitMode.Attack,
            _ => UnitMode.Standby,
        }, RenewalHexSpace.NearestHex(unit.Destination), checked((int)unit.CommandId),
        unit.CastleAttackRange);
}
