namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 한 교전에서의 부대 유효 능력치(design-combat.md "전투값 산출 순서" ①~③의 결과).
/// 상위 계층이 병종 템플릿 + 연구·지형(flat, ②) + 스킬·모드(배수, ③)를 계산해 여기에 채운다.
/// <see cref="AtkStat"/>은 대상 종류(유닛/건물)에 맞는 값을 이미 골라 넣는다(① 판정).
/// </summary>
/// <param name="Troops">현재 병력.</param>
/// <param name="AtkStat">유효 공격 스탯(병종 atk + 연구 + 지형).</param>
/// <param name="DfStat">유효 방어 스탯(병종 df + 연구 + 지형).</param>
/// <param name="AptitudePercent">장수 적성(공격 전용, 100=A+ 기준. A=95 등).</param>
/// <param name="AtkBonusPercent">공격 가산 버킷 배수(100 = 보너스 없음, 130 = +30%).</param>
/// <param name="DfBonusPercent">방어 가산 버킷 배수(100 = 보너스 없음, 클수록 피해↓).</param>
public sealed record CombatStats(
    int Troops,
    int AtkStat,
    int DfStat,
    int AptitudePercent = 100,
    int AtkBonusPercent = 100,
    int DfBonusPercent = 100);
