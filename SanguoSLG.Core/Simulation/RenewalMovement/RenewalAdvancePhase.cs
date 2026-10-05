namespace SanguoSLG.Core.Simulation.RenewalMovement;

/// <summary>Phase 18D 하루의 논리 단계. 화면 연출 시간과 무관하다.</summary>
public enum RenewalAdvancePhase
{
    Movement,
    MovementAftermath,
    Attack,
    AttackAftermath,
    DaySettlement,
    Completed,
}
