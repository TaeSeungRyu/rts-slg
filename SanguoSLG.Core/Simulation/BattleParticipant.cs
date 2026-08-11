namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 전투 페이즈 정산에 들어가는 한 부대의 전투 상태. 산출이 끝난 <see cref="CombatStats"/>,
/// 이동 모드(행군 방어자는 받는 피해 70%·반격 없음), 병력 구성(<see cref="TroopPool"/>)을 묶는다.
/// 액티브·회복 발동은 후속(4c-3)에서 얹는다.
/// </summary>
/// <param name="Stats">유효 능력치(병력 = 라운드 시작 스냅샷).</param>
/// <param name="Mode">이동 모드.</param>
/// <param name="Pool">병력 구성(활성/부상).</param>
public sealed record BattleParticipant(CombatStats Stats, UnitMode Mode, TroopPool Pool);
