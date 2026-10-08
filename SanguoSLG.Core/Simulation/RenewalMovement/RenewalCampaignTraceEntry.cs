namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

public sealed record RenewalCampaignTraceEntry(
    int Day,
    int Tick,
    UnitId Unit,
    RenewalOrderMode Mode,
    ContinuousPosition OriginalDestination,
    ContinuousPosition EffectiveDestination,
    ContinuousPosition Position,
    RenewalTargetId? AssignedTarget,
    RenewalTargetId? PursuitTarget,
    RenewalStopReason StopReason,
    int PathIndex,
    int PathLength);
