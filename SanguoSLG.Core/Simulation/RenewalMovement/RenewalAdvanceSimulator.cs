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

    public RenewalAdvanceSimulator(RenewalMovementMap? movementMap = null) =>
        _movementMap = movementMap;

    public RenewalAdvanceState Start(IEnumerable<RenewalUnitState> units)
    {
        var ordered = AssignArrivalPositions(units.OrderBy(unit => unit.Id.Value).ToList())
            .Select(PreparePath)
            .ToList();
        if (ordered.Select(unit => unit.Id).Distinct().Count() != ordered.Count)
        {
            throw new ArgumentException("부대 ID는 중복될 수 없습니다.", nameof(units));
        }

        return new RenewalAdvanceState(1, RenewalAdvancePhase.Movement, 0, ordered);
    }

    public RenewalStepResult StepMovementTick(RenewalAdvanceState state)
    {
        if (state.Phase != RenewalAdvancePhase.Movement)
        {
            return Empty(state);
        }

        var events = new List<RenewalAdvanceEvent>();
        var nextUnits = new List<RenewalUnitState>(state.Units.Count);
        var nextTick = state.MovementTick + 1;
        foreach (var unit in state.Units.OrderBy(unit => unit.Id.Value))
        {
            var resolved = nextUnits.ToDictionary(x => x.Id);
            var collisionSnapshot = state.Units
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
        if (unit.Arrived || unit.MovementPerDay == 0 || unit.StopReason == RenewalStopReason.NoPath)
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

    private static RenewalUnitState ReachWaypoint(RenewalUnitState unit, ContinuousPosition target)
    {
        if (unit.Path is { Count: > 0 } && unit.PathIndex + 1 < unit.Path.Count)
        {
            return unit with { Position = target, PathIndex = unit.PathIndex + 1 };
        }
        return unit with { Position = unit.ArrivalPosition ?? unit.Destination, Arrived = true };
    }

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
