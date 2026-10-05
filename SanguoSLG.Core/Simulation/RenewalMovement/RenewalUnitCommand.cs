namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

public sealed record RenewalUnitCommand(
    long Id,
    UnitId Unit,
    RenewalOrderMode Mode,
    ContinuousPosition Destination,
    RenewalTargetId? AssignedTarget = null,
    IReadOnlyList<ContinuousPosition>? Waypoints = null);
