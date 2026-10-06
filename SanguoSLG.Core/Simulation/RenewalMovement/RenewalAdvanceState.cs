namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

/// <summary>7일 진행의 현재 날짜·단계·이동 틱과 부대 스냅샷.</summary>
public sealed record RenewalAdvanceState(
    int Day,
    RenewalAdvancePhase Phase,
    int MovementTick,
    IReadOnlyList<RenewalUnitState> Units,
    IReadOnlyList<RenewalTargetState>? ExternalTargets = null,
    IReadOnlyDictionary<UnitId, RenewalCombatProfile>? CombatProfiles = null,
    IReadOnlyList<RenewalStructureCombatState>? Structures = null,
    IReadOnlyList<RenewalSiteCombatState>? Sites = null,
    RenewalIntegrationState? Integration = null)
{
    public bool IsCompleted => Phase == RenewalAdvancePhase.Completed;
}
