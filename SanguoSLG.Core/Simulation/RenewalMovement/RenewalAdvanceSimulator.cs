namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

/// <summary>
/// Phase 18D 독립 검수용 최소 진행기. 이동 경로·충돌·전투 공식은 후속 단계에서 연결하고,
/// 여기서는 고정 틱 연속 위치와 하루 5단계·7일 정지 계약만 소유한다.
/// </summary>
public sealed class RenewalAdvanceSimulator
{
    public const int MovementTicksPerDay = 50;
    public const int DaysPerAdvance = 7;
    public const long ArrivalTolerance = 50;
    public const long UnitCollisionRadius = 180;
    public const long ArrivalDispersionLimit = 500;

    private readonly RenewalMovementMap? _movementMap;
    private readonly RenewalCombatPhaseService? _combat;

    public RenewalAdvanceSimulator(RenewalMovementMap? movementMap = null,
        RenewalCombatPhaseService? combat = null)
    {
        _movementMap = movementMap;
        _combat = combat;
    }

    public RenewalAdvanceState Start(IEnumerable<RenewalUnitState> units,
        IEnumerable<RenewalTargetState>? externalTargets = null)
    {
        var ordered = AssignArrivalPositions(units.OrderBy(unit => unit.Id.Value).ToList())
            .Select(PreparePath)
            .ToList();
        if (ordered.Select(unit => unit.Id).Distinct().Count() != ordered.Count)
        {
            throw new ArgumentException("부대 ID는 중복될 수 없습니다.", nameof(units));
        }

        return new RenewalAdvanceState(1, RenewalAdvancePhase.Movement, 0, ordered,
            externalTargets?.OrderBy(x => x.Id.Kind).ThenBy(x => x.Id.Value).ToList());
    }

    public RenewalStepResult StepMovementTick(RenewalAdvanceState state)
    {
        if (state.Phase != RenewalAdvancePhase.Movement)
        {
            return Empty(state);
        }

        var events = new List<RenewalAdvanceEvent>();
        var reconciled = ReconcileOrders(state.Units, Targets(state),
            state.Day, state.MovementTick + 1, events);
        var nextUnits = new List<RenewalUnitState>(state.Units.Count);
        var nextTick = state.MovementTick + 1;
        foreach (var unit in reconciled.OrderBy(unit => unit.Id.Value))
        {
            var resolved = nextUnits.ToDictionary(x => x.Id);
            var collisionSnapshot = reconciled
                .Select(x => resolved.TryGetValue(x.Id, out var updated) ? updated : x)
                .ToList();
            var moved = MoveOneTick(unit, collisionSnapshot);
            nextUnits.Add(moved);
            if (moved.Position != unit.Position)
            {
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.UnitMoved,
                    state.Day, RenewalAdvancePhase.Movement, nextTick, unit.Id,
                    unit.Position, moved.Position));
            }
            if (!unit.Arrived && moved.Arrived)
            {
                var kind = moved.Position == moved.Destination
                    ? RenewalAdvanceEventKind.UnitArrived
                    : RenewalAdvanceEventKind.UnitDispersed;
                events.Add(new RenewalAdvanceEvent(kind,
                    state.Day, RenewalAdvancePhase.Movement, nextTick, unit.Id,
                    unit.Position, moved.Position));
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.OrderCompleted,
                    state.Day, RenewalAdvancePhase.Movement, nextTick, unit.Id,
                    unit.Position, moved.Position));
            }
            else if (unit.StopReason == RenewalStopReason.None
                && moved.StopReason != RenewalStopReason.None)
            {
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.UnitBlocked,
                    state.Day, RenewalAdvancePhase.Movement, nextTick, unit.Id,
                    unit.Position, moved.Position, moved.StopReason));
            }
        }

        var phase = nextTick >= MovementTicksPerDay
            ? RenewalAdvancePhase.MovementAftermath
            : RenewalAdvancePhase.Movement;
        var next = state with { MovementTick = nextTick, Phase = phase, Units = nextUnits };
        if (phase != state.Phase)
        {
            events.Add(PhaseEvent(next));
        }

        return new RenewalStepResult(next, events);
    }

    public RenewalStepResult StepPhase(RenewalAdvanceState state)
    {
        if (state.IsCompleted)
        {
            return Empty(state);
        }

        if (state.Phase == RenewalAdvancePhase.Movement)
        {
            var current = state;
            var events = new List<RenewalAdvanceEvent>();
            while (current.Phase == RenewalAdvancePhase.Movement)
            {
                var tick = StepMovementTick(current);
                current = tick.State;
                events.AddRange(tick.Events);
            }

            return new RenewalStepResult(current, events);
        }

        if (state.Phase == RenewalAdvancePhase.DaySettlement)
        {
            return CompleteDay(state);
        }

        if (state.Phase == RenewalAdvancePhase.Attack && _combat is not null)
        {
            var resolved = _combat.Resolve(state);
            var attackNext = resolved.State with { Phase = RenewalAdvancePhase.AttackAftermath };
            return new RenewalStepResult(attackNext,
                resolved.Events.Append(PhaseEvent(attackNext)).ToList());
        }

        var nextPhase = state.Phase switch
        {
            RenewalAdvancePhase.MovementAftermath => RenewalAdvancePhase.Attack,
            RenewalAdvancePhase.Attack => RenewalAdvancePhase.AttackAftermath,
            RenewalAdvancePhase.AttackAftermath => RenewalAdvancePhase.DaySettlement,
            _ => throw new InvalidOperationException($"지원하지 않는 단계 전이: {state.Phase}"),
        };
        var next = state with { Phase = nextPhase };
        return new RenewalStepResult(next, [PhaseEvent(next)]);
    }

    public RenewalStepResult StepDay(RenewalAdvanceState state)
    {
        if (state.IsCompleted)
        {
            return Empty(state);
        }

        var startDay = state.Day;
        var current = state;
        var events = new List<RenewalAdvanceEvent>();
        while (!current.IsCompleted && current.Day == startDay)
        {
            var step = StepPhase(current);
            current = step.State;
            events.AddRange(step.Events);
        }

        return new RenewalStepResult(current, events);
    }

    public RenewalStepResult RunToCompletion(RenewalAdvanceState state)
    {
        var current = state;
        var events = new List<RenewalAdvanceEvent>();
        while (!current.IsCompleted)
        {
            var day = StepDay(current);
            current = day.State;
            events.AddRange(day.Events);
        }

        return new RenewalStepResult(current, events);
    }

    public (RenewalAdvanceState State, RenewalFixedStepClock Clock, IReadOnlyList<RenewalAdvanceEvent> Events)
        AdvanceElapsed(RenewalAdvanceState state, RenewalFixedStepClock clock, long elapsedMicroseconds)
    {
        if (elapsedMicroseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedMicroseconds));
        }

        if (state.Phase != RenewalAdvancePhase.Movement)
        {
            return (state, new RenewalFixedStepClock(), Array.Empty<RenewalAdvanceEvent>());
        }

        var pending = checked(clock.PendingMicroseconds + elapsedMicroseconds);
        var current = state;
        var events = new List<RenewalAdvanceEvent>();
        while (pending >= RenewalFixedStepClock.TickMicroseconds
            && current.Phase == RenewalAdvancePhase.Movement)
        {
            pending -= RenewalFixedStepClock.TickMicroseconds;
            var step = StepMovementTick(current);
            current = step.State;
            events.AddRange(step.Events);
        }

        if (current.Phase != RenewalAdvancePhase.Movement)
        {
            pending = 0;
        }

        return (current, new RenewalFixedStepClock(pending), events);
    }

    private static RenewalStepResult CompleteDay(RenewalAdvanceState state)
    {
        var events = new List<RenewalAdvanceEvent>
        {
            new(RenewalAdvanceEventKind.DayCompleted, state.Day,
                RenewalAdvancePhase.DaySettlement, state.MovementTick),
        };
        if (state.Day >= DaysPerAdvance)
        {
            var completed = state with { Phase = RenewalAdvancePhase.Completed };
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.AdvanceCompleted,
                state.Day, RenewalAdvancePhase.Completed, state.MovementTick));
            return new RenewalStepResult(completed, events);
        }

        var next = state with
        {
            Day = state.Day + 1,
            Phase = RenewalAdvancePhase.Movement,
            MovementTick = 0,
        };
        events.Add(PhaseEvent(next));
        return new RenewalStepResult(next, events);
    }

    private RenewalUnitState PreparePath(RenewalUnitState unit)
    {
        if (_movementMap is null || unit.Arrived)
        {
            return unit;
        }
        var path = _movementMap.FindPath(unit.Domain, unit.Position,
            unit.ArrivalPosition ?? unit.Destination);
        return path.Count == 0
            ? unit with { Path = path, StopReason = RenewalStopReason.NoPath }
            : unit with { Path = path, PathIndex = 0, StopReason = RenewalStopReason.None };
    }

    private RenewalUnitState MoveOneTick(RenewalUnitState unit,
        IReadOnlyList<RenewalUnitState> allUnits)
    {
        if (!unit.IsActive || unit.Mode == RenewalOrderMode.Standby || unit.Arrived
            || unit.MovementPerDay == 0
            || unit.StopReason is RenewalStopReason.NoPath or RenewalStopReason.TargetInRange)
        {
            return unit;
        }

        var speedPercent = _movementMap?.SpeedPercentAt(unit.Position, UnitCollisionRadius) ?? 100;
        var numerator = checked(unit.MovementPerDay * (int)ContinuousPosition.UnitsPerTile
            * speedPercent + unit.MovementRemainder);
        var divisor = MovementTicksPerDay * 100;
        var remaining = (long)(numerator / divisor);
        var remainder = numerator % divisor;
        var current = unit with { MovementRemainder = remainder, StopReason = RenewalStopReason.None };
        while (remaining > 0 && !current.Arrived)
        {
            var target = current.Path is { Count: > 0 } && current.PathIndex < current.Path.Count
                ? current.Path[current.PathIndex]
                : current.ArrivalPosition ?? current.Destination;
            var dx = target.X - current.Position.X;
            var dy = target.Y - current.Position.Y;
            var distanceSquared = checked(dx * dx + dy * dy);
            var distance = IntegerMath.SquareRoot(distanceSquared);
            var finalWaypoint = current.Path is not { Count: > 0 }
                || current.PathIndex >= current.Path.Count - 1;
            if (finalWaypoint && distance <= ArrivalTolerance)
            {
                current = ReachWaypoint(current, target);
                continue;
            }

            var reachesTarget = distance <= remaining;
            var used = reachesTarget ? distance : remaining;
            var moveX = reachesTarget ? dx : DivideRounded(checked(dx * used), distance);
            var moveY = reachesTarget ? dy : DivideRounded(checked(dy * used), distance);
            if (moveX == 0 && dx != 0)
            {
                moveX = Math.Sign(dx);
            }
            if (moveY == 0 && dy != 0)
            {
                moveY = Math.Sign(dy);
            }

            var candidate = new ContinuousPosition(current.Position.X + moveX,
                current.Position.Y + moveY);
            var collision = _movementMap?.FirstStaticCollision(current.Position, candidate,
                UnitCollisionRadius, current.Domain) ?? RenewalStopReason.None;
            if (collision == RenewalStopReason.None)
            {
                collision = FirstEnemyCollision(current, candidate, allUnits);
            }
            if (collision != RenewalStopReason.None)
            {
                return current with { StopReason = collision };
            }

            current = reachesTarget
                ? ReachWaypoint(current, target)
                : current with { Position = candidate };
            remaining -= used;
        }

        return current;
    }

    private static long DivideRounded(long value, long divisor) => value >= 0
        ? (value + divisor / 2) / divisor
        : (value - divisor / 2) / divisor;

    private static RenewalStopReason FirstEnemyCollision(RenewalUnitState unit,
        ContinuousPosition candidate, IReadOnlyList<RenewalUnitState> allUnits)
    {
        foreach (var other in allUnits.OrderBy(x => x.Id.Value))
        {
            if (other.Id == unit.Id || other.Owner == unit.Owner)
            {
                continue;
            }
            if (candidate.DistanceSquaredTo(other.Position) < unit.Position.DistanceSquaredTo(other.Position)
                && RenewalMovementMap.SegmentTouchesCircle(unit.Position, candidate, other.Position,
                    UnitCollisionRadius * 2))
            {
                return RenewalStopReason.EnemyBlocked;
            }
        }
        return RenewalStopReason.None;
    }

    private RenewalUnitState ReachWaypoint(RenewalUnitState unit, ContinuousPosition target)
    {
        if (unit.Path is { Count: > 0 } && unit.PathIndex + 1 < unit.Path.Count)
        {
            return unit with { Position = target, PathIndex = unit.PathIndex + 1 };
        }
        if (unit.Mode is RenewalOrderMode.March or RenewalOrderMode.Advance
            && unit.PursuitTarget is null
            && unit.OriginalWaypoints is { Count: > 0 } waypoints
            && unit.OriginalWaypointIndex < waypoints.Count
            && unit.Destination == waypoints[unit.OriginalWaypointIndex])
        {
            var nextIndex = unit.OriginalWaypointIndex + 1;
            var nextDestination = nextIndex < waypoints.Count
                ? waypoints[nextIndex]
                : unit.OriginalDestination ?? unit.Destination;
            return Retarget(unit with
            {
                Position = target,
                OriginalWaypointIndex = nextIndex,
                Path = null,
                PathIndex = 0,
            }, nextDestination);
        }

        return unit with
        {
            Position = unit.ArrivalPosition ?? unit.Destination,
            Arrived = true,
            Mode = RenewalOrderMode.Standby,
            PursuitTarget = null,
            StopReason = unit.StopReason == RenewalStopReason.TargetLost
                ? RenewalStopReason.TargetLost
                : RenewalStopReason.None,
        };
    }

    private IReadOnlyList<RenewalUnitState> ReconcileOrders(
        IReadOnlyList<RenewalUnitState> units,
        IReadOnlyList<RenewalTargetState> targets, int day, int movementTick,
        ICollection<RenewalAdvanceEvent> events)
    {
        var snapshot = units.OrderBy(x => x.Id.Value).ToList();
        return snapshot.Select(unit => ReconcileOrder(unit, targets, day, movementTick, events))
            .ToList();
    }

    private RenewalUnitState ReconcileOrder(RenewalUnitState unit,
        IReadOnlyList<RenewalTargetState> targets, int day, int movementTick,
        ICollection<RenewalAdvanceEvent> events)
    {
        if (!unit.IsActive || unit.Mode is RenewalOrderMode.Standby or RenewalOrderMode.March)
        {
            return unit;
        }

        if (unit.Mode == RenewalOrderMode.Advance)
        {
            var current = FindHostile(targets, unit, unit.PursuitTarget);
            if (current is not null && IsVisibleTo(unit, current))
            {
                return FollowTarget(unit, current, day, movementTick);
            }

            if (unit.PursuitTarget is not null)
            {
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.TargetLost,
                    day, RenewalAdvancePhase.Movement, movementTick, unit.Id,
                    unit.Position, NextOriginalDestination(unit),
                    RenewalStopReason.TargetLost, unit.PursuitTarget));
                return Retarget(unit with
                {
                    PursuitTarget = null,
                    LastKnownTargetPosition = null,
                    StopReason = RenewalStopReason.None,
                    AttackRangeReachedTick = null,
                }, NextOriginalDestination(unit));
            }

            var acquired = targets
                .Where(candidate => IsHostile(unit, candidate) && IsVisibleTo(unit, candidate))
                .OrderBy(candidate => unit.Position.DistanceSquaredTo(candidate.Position))
                .ThenBy(candidate => candidate.SelectionOrder)
                .ThenBy(candidate => candidate.Id.Kind)
                .ThenBy(candidate => candidate.Id.Value)
                .FirstOrDefault();
            if (acquired is not null)
            {
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.TargetAcquired,
                    day, RenewalAdvancePhase.Movement, movementTick, unit.Id,
                    unit.Position, acquired.Position, Target: acquired.Id));
                return FollowTarget(unit with { PursuitTarget = acquired.Id }, acquired,
                    day, movementTick);
            }

            return Retarget(unit with
            {
                PursuitTarget = null,
                LastKnownTargetPosition = null,
                StopReason = RenewalStopReason.None,
                AttackRangeReachedTick = null,
            }, NextOriginalDestination(unit));
        }

        var assigned = FindHostile(targets, unit, unit.AssignedTarget);
        if (assigned is not null && IsVisibleTo(unit, assigned))
        {
            return FollowTarget(unit, assigned, day, movementTick);
        }

        if (assigned is null)
        {
            if (unit.AssignedTarget is not null)
            {
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.TargetLost,
                    day, RenewalAdvancePhase.Movement, movementTick, unit.Id,
                    unit.Position, unit.Position, RenewalStopReason.TargetLost,
                    unit.AssignedTarget));
            }
            return ToStandby(unit, RenewalStopReason.TargetLost);
        }

        if (unit.LastKnownTargetPosition is not { } lastKnown)
        {
            return ToStandby(unit, RenewalStopReason.TargetLost);
        }
        var lastKnownReached = unit.Position.DistanceTo(lastKnown) <= ArrivalTolerance
            || unit.StopReason == RenewalStopReason.EnemyBlocked;
        if (lastKnownReached)
        {
            events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.TargetLost,
                day, RenewalAdvancePhase.Movement, movementTick, unit.Id,
                unit.Position, lastKnown, RenewalStopReason.TargetLost, unit.AssignedTarget));
            return ToStandby(unit, RenewalStopReason.TargetLost);
        }
        return Retarget(unit with { StopReason = RenewalStopReason.TargetLost }, lastKnown);
    }

    private RenewalUnitState FollowTarget(RenewalUnitState unit, RenewalTargetState target,
        int day, int movementTick)
    {
        var range = Math.Max(0, unit.AttackRange) * ContinuousPosition.UnitsPerTile;
        if (unit.Position.DistanceTo(target.Position) <= range)
        {
            return unit with
            {
                Destination = target.Position,
                LastKnownTargetPosition = target.Position,
                Arrived = false,
                StopReason = RenewalStopReason.TargetInRange,
                AttackRangeReachedTick = unit.AttackRangeReachedTick
                    ?? checked((day - 1) * MovementTicksPerDay + movementTick),
            };
        }
        return Retarget(unit with
        {
            LastKnownTargetPosition = target.Position,
            StopReason = RenewalStopReason.None,
            AttackRangeReachedTick = null,
        }, target.Position);
    }

    private RenewalUnitState Retarget(RenewalUnitState unit, ContinuousPosition destination)
    {
        if (unit.Destination == destination && unit.Path is not null && !unit.Arrived)
        {
            return unit;
        }
        return PreparePath(unit with
        {
            Destination = destination,
            ArrivalPosition = null,
            Arrived = false,
            Path = null,
            PathIndex = 0,
        });
    }

    private static ContinuousPosition NextOriginalDestination(RenewalUnitState unit)
    {
        if (unit.OriginalWaypoints is { Count: > 0 } waypoints
            && unit.OriginalWaypointIndex < waypoints.Count)
        {
            return waypoints[unit.OriginalWaypointIndex];
        }
        return unit.OriginalDestination ?? unit.Destination;
    }

    private static RenewalUnitState ToStandby(RenewalUnitState unit, RenewalStopReason reason) =>
        unit with
        {
            Mode = RenewalOrderMode.Standby,
            PursuitTarget = null,
            Arrived = true,
            StopReason = reason,
            AttackRangeReachedTick = null,
            Path = null,
            PathIndex = 0,
        };

    private static RenewalTargetState? FindHostile(IReadOnlyList<RenewalTargetState> targets,
        RenewalUnitState observer, RenewalTargetId? target) => target is not { } targetId
        ? null
        : targets.FirstOrDefault(candidate => candidate.Id == targetId && IsHostile(observer, candidate));

    private static bool IsHostile(RenewalUnitState observer, RenewalTargetState candidate) =>
        candidate.Id != RenewalTargetId.ForUnit(observer.Id)
        && candidate.IsActive && candidate.Owner != observer.Owner;

    private static bool IsVisibleTo(RenewalUnitState observer, RenewalTargetState candidate) =>
        candidate.IsVisible && observer.Position.DistanceTo(candidate.Position)
            <= Math.Max(0, observer.DetectionRange) * ContinuousPosition.UnitsPerTile;

    private static RenewalTargetState ToTarget(RenewalUnitState unit) => new(
        RenewalTargetId.ForUnit(unit.Id), unit.Owner, unit.Position, unit.IsActive,
        unit.IsVisible, unit.CommandId);

    private static IReadOnlyList<RenewalTargetState> Targets(RenewalAdvanceState state) =>
        state.Units.Select(ToTarget)
            .Concat(state.ExternalTargets ?? Array.Empty<RenewalTargetState>())
            .Concat((state.Structures ?? Array.Empty<RenewalStructureCombatState>()).Select(x =>
                new RenewalTargetState(x.Id, x.Owner, x.Position, x.IsActive, x.IsVisible)))
            .Concat((state.Sites ?? Array.Empty<RenewalSiteCombatState>()).Select(x =>
                new RenewalTargetState(x.Id, x.Owner, x.Position, true, x.IsVisible)))
            .OrderBy(x => x.Id.Kind).ThenBy(x => x.Id.Value).ToList();

    private IReadOnlyList<RenewalUnitState> AssignArrivalPositions(IReadOnlyList<RenewalUnitState> units)
    {
        if (_movementMap is null)
        {
            return units;
        }

        var result = new List<RenewalUnitState>(units.Count);
        foreach (var group in units.GroupBy(x => x.Destination))
        {
            var occupied = new List<ContinuousPosition>();
            foreach (var unit in group.OrderBy(x => x.Id.Value))
            {
                var arrival = unit.Destination;
                if (_movementMap.CanStand(unit.Domain, unit.Destination, UnitCollisionRadius))
                {
                    foreach (var candidate in DispersionCandidates(unit.Destination))
                    {
                        if (_movementMap.CanStand(unit.Domain, candidate, UnitCollisionRadius)
                            && occupied.All(existing => existing.DistanceSquaredTo(candidate)
                                >= checked(UnitCollisionRadius * 2 * UnitCollisionRadius * 2)))
                        {
                            arrival = candidate;
                            break;
                        }
                    }
                }
                occupied.Add(arrival);
                result.Add(unit with { ArrivalPosition = arrival });
            }
        }
        return result.OrderBy(x => x.Id.Value).ToList();
    }

    private static IEnumerable<ContinuousPosition> DispersionCandidates(ContinuousPosition origin)
    {
        yield return origin;
        yield return new ContinuousPosition(origin.X + 433, origin.Y + 250);
        yield return new ContinuousPosition(origin.X, origin.Y + ArrivalDispersionLimit);
        yield return new ContinuousPosition(origin.X - 433, origin.Y + 250);
        yield return new ContinuousPosition(origin.X - 433, origin.Y - 250);
        yield return new ContinuousPosition(origin.X, origin.Y - ArrivalDispersionLimit);
        yield return new ContinuousPosition(origin.X + 433, origin.Y - 250);
    }

    private static RenewalAdvanceEvent PhaseEvent(RenewalAdvanceState state)
        => new(RenewalAdvanceEventKind.PhaseChanged, state.Day, state.Phase, state.MovementTick);

    private static RenewalStepResult Empty(RenewalAdvanceState state)
        => new(state, Array.Empty<RenewalAdvanceEvent>());
}
