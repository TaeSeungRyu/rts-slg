namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 전투 페이즈 정산에 들어가는 한 부대의 전투 상태. 산출이 끝난 <see cref="CombatStats"/>,
/// 이동 모드(행군 방어자는 받는 피해 70%·반격 없음), 병력 구성(<see cref="TroopPool"/>)을 묶는다.
/// 액티브·회복 발동은 후속(4c-3)에서 얹는다.
/// </summary>
/// <param name="Stats">유효 능력치(계략 디버프까지 반영된 값). 공격 병력은 정산 시 활성+회복분으로 덮는다.</param>
/// <param name="Mode">이동 모드.</param>
/// <param name="Pool">병력 구성(활성/부상).</param>
/// <param name="Might">선봉 무력(타격·방어 액티브 스케일).</param>
/// <param name="Intellect">선봉 지력(회복 액티브 스케일).</param>
/// <param name="MaxTroops">최대 병력(회복 상한). 0이면 회복 없음.</param>
/// <param name="StrikeActive">발동한 타격 액티브(주대상 공격 대체).</param>
/// <param name="DefenseActive">발동한 방어 액티브(받는 피해 감소).</param>
/// <param name="HealActive">발동한 회복 액티브(부상 풀에서 병력 회복).</param>
/// <param name="TargetIsBuilding">대상이 건물인가(분쇄 등).</param>
public sealed record BattleParticipant(
    CombatStats Stats,
    UnitMode Mode,
    TroopPool Pool,
    int Might = 60,
    int Intellect = 60,
    int MaxTroops = 0,
    ActiveSkill? StrikeActive = null,
    ActiveSkill? DefenseActive = null,
    ActiveSkill? HealActive = null,
    bool TargetIsBuilding = false);
