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

    public RenewalAdvanceState Start(IEnumerable<RenewalUnitState> units)
    {
        var ordered = units.OrderBy(unit => unit.Id.Value).ToList();
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
            var moved = MoveOneTick(unit);
            nextUnits.Add(moved);
            if (moved.Position != unit.Position)
            {
                events.Add(new RenewalAdvanceEvent(RenewalAdvanceEventKind.UnitMoved,
                    state.Day, RenewalAdvancePhase.Movement, nextTick, unit.Id,
                    unit.Position, moved.Position));
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

    private static RenewalUnitState MoveOneTick(RenewalUnitState unit)
    {
        if (unit.Arrived || unit.MovementPerDay == 0)
        {
            return unit;
        }

        var dx = unit.Destination.X - unit.Position.X;
        var dy = unit.Destination.Y - unit.Position.Y;
        var distanceSquared = checked(dx * dx + dy * dy);
        if (distanceSquared <= ArrivalTolerance * ArrivalTolerance)
        {
            return unit with { Position = unit.Destination, Arrived = true };
        }

        var distance = IntegerSquareRoot(distanceSquared);
        var step = checked((long)unit.MovementPerDay * ContinuousPosition.UnitsPerTile
            / MovementTicksPerDay);
        if (distance <= step)
        {
            return unit with { Position = unit.Destination, Arrived = true };
        }

        var moveX = dx * step / distance;
        var moveY = dy * step / distance;
        if (moveX == 0 && dx != 0)
        {
            moveX = Math.Sign(dx);
        }
        if (moveY == 0 && dy != 0)
        {
            moveY = Math.Sign(dy);
        }

        return unit with
        {
            Position = new ContinuousPosition(unit.Position.X + moveX, unit.Position.Y + moveY),
        };
    }

    private static long IntegerSquareRoot(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var root = (long)Math.Sqrt(value);
        while (checked((root + 1) * (root + 1)) <= value)
        {
            root++;
        }
        while (root * root > value)
        {
            root--;
        }
        return root;
    }

    private static RenewalAdvanceEvent PhaseEvent(RenewalAdvanceState state)
        => new(RenewalAdvanceEventKind.PhaseChanged, state.Day, state.Phase, state.MovementTick);

    private static RenewalStepResult Empty(RenewalAdvanceState state)
        => new(state, Array.Empty<RenewalAdvanceEvent>());
}
