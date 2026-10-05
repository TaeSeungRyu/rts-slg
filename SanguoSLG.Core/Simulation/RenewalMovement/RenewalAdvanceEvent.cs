namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

/// <summary>한 고정 틱 또는 단계 전이에서 발생한 결정론적 사건.</summary>
public sealed record RenewalAdvanceEvent(
    RenewalAdvanceEventKind Kind,
    int Day,
    RenewalAdvancePhase Phase,
    int MovementTick,
    UnitId? Unit = null,
    ContinuousPosition? From = null,
    ContinuousPosition? To = null);
