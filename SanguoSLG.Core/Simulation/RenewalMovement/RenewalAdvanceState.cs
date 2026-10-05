namespace SanguoSLG.Core.Simulation.RenewalMovement;

/// <summary>7일 진행의 현재 날짜·단계·이동 틱과 부대 스냅샷.</summary>
public sealed record RenewalAdvanceState(
    int Day,
    RenewalAdvancePhase Phase,
    int MovementTick,
    IReadOnlyList<RenewalUnitState> Units)
{
    public bool IsCompleted => Phase == RenewalAdvancePhase.Completed;
}
