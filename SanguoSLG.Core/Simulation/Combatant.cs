namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 한 교전에 들어가는 한쪽 부대(EngagementResolver 입력). 산출 ①~③(패시브 가산 포함)이 끝난
/// <see cref="CombatStats"/>에, 이번 교전에 발동하는 액티브와 스탯을 얹는다. 계략 디버프는 이미
/// <see cref="Stats"/>에 반영된 상태로 넘긴다(계략은 2일 전 시전분이 이 교전에서 발현).
/// </summary>
/// <param name="Stats">산출된 유효 능력치.</param>
/// <param name="MaxTroops">최대 병력(회복 상한 계산용).</param>
/// <param name="Might">선봉 무력(타격·방어 액티브 스케일).</param>
/// <param name="Intellect">선봉 지력(회복 액티브 스케일).</param>
/// <param name="StrikeActive">발동한 타격 액티브(있으면 일반 공격 대체).</param>
/// <param name="DefenseActive">발동한 방어 액티브(받는 피해 감소).</param>
/// <param name="HealActive">발동한 회복 액티브(병력 회복).</param>
/// <param name="TargetIsBuilding">대상이 건물인가(분쇄 등).</param>
public sealed record Combatant(
    CombatStats Stats,
    int MaxTroops,
    int Might = 60,
    int Intellect = 60,
    ActiveSkill? StrikeActive = null,
    ActiveSkill? DefenseActive = null,
    ActiveSkill? HealActive = null,
    bool TargetIsBuilding = false);
