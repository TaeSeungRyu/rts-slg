namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 성·항구를 공격하는 한 부대(design-combat.md "성 전투"). 성벽 단계에선 <see cref="AtkBuilding"/>로
/// 성벽을 치고, 성벽이 무너진 뒤엔 <see cref="AtkUnit"/>로 수비 병력을 직접 친다. <see cref="Df"/>는
/// 성의 반격을 받을 때 쓰인다.
/// </summary>
/// <param name="Troops">현재 병력.</param>
/// <param name="AtkBuilding">건물dmg(성벽 단계).</param>
/// <param name="AtkUnit">유닛dmg(성벽 붕괴 후).</param>
/// <param name="Df">방어력(성 반격을 받을 때).</param>
/// <param name="AptitudePercent">공격 적성(공성/해상 분류).</param>
/// <param name="AtkBonusPercent">공격 가산 배수(100=없음).</param>
/// <param name="DfBonusPercent">방어 가산 배수(100=없음).</param>
/// <param name="InCounterRange">성 반격 사거리(무조건 1) 안인가. 사거리 2 병종(투석기·공성탑·화랑궁병)은 false.</param>
public sealed record SiegeAttacker(
    int Troops,
    int AtkBuilding,
    int AtkUnit,
    int Df,
    int AptitudePercent = 100,
    int AtkBonusPercent = 100,
    int DfBonusPercent = 100,
    bool InCounterRange = true);
