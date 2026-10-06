namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>독립 검수장에서 사용하는 최소 연속 이동 부대 상태.</summary>
public sealed record RenewalUnitState(
    UnitId Id,
    ContinuousPosition Position,
    ContinuousPosition Destination,
    int MovementPerDay,
    bool Arrived = false,
    FactionId Owner = default,
    MovementDomain Domain = MovementDomain.Land,
    IReadOnlyList<ContinuousPosition>? Path = null,
    int PathIndex = 0,
    int MovementRemainder = 0,
    RenewalStopReason StopReason = RenewalStopReason.None,
    ContinuousPosition? ArrivalPosition = null,
    RenewalOrderMode Mode = RenewalOrderMode.March,
    ContinuousPosition? OriginalDestination = null,
    IReadOnlyList<ContinuousPosition>? OriginalWaypoints = null,
    int OriginalWaypointIndex = 0,
    RenewalTargetId? AssignedTarget = null,
    RenewalTargetId? PursuitTarget = null,
    ContinuousPosition? LastKnownTargetPosition = null,
    int DetectionRange = 3,
    int AttackRange = 1,
    bool IsActive = true,
    bool IsVisible = true,
    long CommandId = 0,
    int? AttackRangeReachedTick = null,
    int BuildingAttackRange = 1,
    int CastleAttackRange = 1,
    RenewalTargetId? GarrisonStructure = null)
{
    public static RenewalUnitState Create(UnitId id, ContinuousPosition position,
        ContinuousPosition destination, int movementPerDay)
    {
        if (movementPerDay < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(movementPerDay));
        }

        return new RenewalUnitState(id, position, destination, movementPerDay,
            position == destination, new FactionId(1));
    }
}
