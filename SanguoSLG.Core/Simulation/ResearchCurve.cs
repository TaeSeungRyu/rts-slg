namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 병종 연구 10단계 곡선(design-combat.md "병종 연구"). 공격·방어 스탯에 flat으로 더해지는 누적 보정.
/// 1~8단계 각 +1, 9단계 +2, 10단계 +3 → 누적 +1…+13. 9단계 누적 +10이 옛 "풀연구 +10"과 일치.
/// </summary>
public static class ResearchCurve
{
    /// <summary>연구 단계(0=미연구 … 10)의 누적 공/방 보정.</summary>
    public static int Bonus(int level) => level switch
    {
        <= 0 => 0,
        <= 8 => level,   // 1~8단계: +1씩
        9 => 10,         // 9단계: +2 (누적 10)
        _ => 13,         // 10단계 이상: +3 (누적 13)
    };
}
