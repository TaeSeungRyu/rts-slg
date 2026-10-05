namespace SanguoSLG.Core.Simulation.RenewalMovement;

/// <summary>단계 진행 뒤의 새 상태와 순서가 고정된 사건 목록.</summary>
public sealed record RenewalStepResult(
    RenewalAdvanceState State,
    IReadOnlyList<RenewalAdvanceEvent> Events);
