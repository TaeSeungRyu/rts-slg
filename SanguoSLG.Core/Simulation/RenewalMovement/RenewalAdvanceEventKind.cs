namespace SanguoSLG.Core.Simulation.RenewalMovement;

/// <summary>검수장과 이후 표현 계층이 소비하는 단계 진행 사건 종류.</summary>
public enum RenewalAdvanceEventKind
{
    UnitMoved,
    UnitArrived,
    UnitBlocked,
    UnitDispersed,
    TargetAcquired,
    TargetLost,
    OrderCompleted,
    AttackResolved,
    CombatParticipationRecorded,
    ActiveSkillFired,
    StatusTicked,
    StatusApplied,
    UnitDefeated,
    LootTransferred,
    StructureDamaged,
    StructureDestroyed,
    SiteDamaged,
    SiteCaptured,
    SiegeWaiting,
    DailySettlementApplied,
    WeeklySettlementApplied,
    ScheduledWorkCompleted,
    ProvisionsConsumed,
    SupplyTransferred,
    PhaseChanged,
    DayCompleted,
    AdvanceCompleted,
}
