namespace SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalCommandService
{
    private readonly RenewalMovementMap? _map;

    public RenewalCommandService(RenewalMovementMap? map = null) => _map = map;

    public RenewalAdvanceState Apply(RenewalAdvanceState state, RenewalUnitCommand command)
    {
        var index = state.Units.ToList().FindIndex(x => x.Id == command.Unit);
        if (index < 0)
        {
            throw new InvalidOperationException($"명령 대상 부대를 찾을 수 없습니다: {command.Unit}");
        }

        var unit = state.Units[index];
        if (command.Id <= unit.CommandId)
        {
            return state;
        }
        if (command.Mode == RenewalOrderMode.Attack && command.AssignedTarget is null)
        {
            throw new ArgumentException("공격 명령에는 지정 대상이 필요합니다.", nameof(command));
        }

        var target = command.AssignedTarget is { } targetId
            ? Targets(state).FirstOrDefault(x => x.Id == targetId)
            : null;
        var waypoints = command.Waypoints ?? Array.Empty<ContinuousPosition>();
        var destination = target?.Position ?? waypoints.FirstOrDefault(command.Destination);
        var path = _map?.FindPath(unit.Domain, unit.Position, destination);
        var updated = unit with
        {
            CommandId = command.Id,
            Mode = command.Mode,
            OriginalDestination = command.Destination,
            OriginalWaypoints = waypoints,
            OriginalWaypointIndex = 0,
            Destination = destination,
            AssignedTarget = command.AssignedTarget,
            PursuitTarget = null,
            LastKnownTargetPosition = target?.Position,
            Arrived = command.Mode == RenewalOrderMode.Standby,
            ArrivalPosition = null,
            Path = path,
            PathIndex = 0,
            StopReason = _map is not null && path is { Count: 0 }
                ? RenewalStopReason.NoPath
                : RenewalStopReason.None,
            AttackRangeReachedTick = null,
        };
        var units = state.Units.ToList();
        units[index] = updated;
        return state with { Units = units };
    }

    private static IEnumerable<RenewalTargetState> Targets(RenewalAdvanceState state) =>
        state.Units.Select(unit => new RenewalTargetState(
            RenewalTargetId.ForUnit(unit.Id), unit.Owner, unit.Position, unit.IsActive, unit.IsVisible))
        .Concat(state.ExternalTargets ?? Array.Empty<RenewalTargetState>())
        .Concat((state.Structures ?? Array.Empty<RenewalStructureCombatState>()).Select(x =>
            new RenewalTargetState(x.Id, x.Owner, x.Position, x.IsActive, x.IsVisible)))
        .Concat((state.Sites ?? Array.Empty<RenewalSiteCombatState>()).Select(x =>
            new RenewalTargetState(x.Id, x.Owner, x.Position, true, x.IsVisible)));
}
