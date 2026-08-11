namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 액티브 위력의 스탯 연동 배수(design-skill-actives.md "위력 연동 공식").
/// M = clamp(1 + (스탯 − 60)/100, 0.5, 1.5). 정수 퍼센트로 반환(무력 80 → 120).
/// 타격·방어형은 무력, 회복형은 지력을 넣는다.
/// </summary>
public static class StatScale
{
    /// <summary>스탯(무력 또는 지력)의 위력 배수(퍼센트, 60 → 100).</summary>
    public static int Percent(int stat) => System.Math.Clamp(100 + (stat - 60), 50, 150);
}
