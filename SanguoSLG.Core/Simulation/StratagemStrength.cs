namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 계략 강도 배율(design-stratagem.md "결정론 강도"). 배율 = clamp(1 + (시전지력 − 대상지력)/100, 0.3, 2.0).
/// 정수 퍼센트로(지력차 +50 → 150). 항상 발동하되 대상 지력이 높으면 약해진다(하한 있음).
/// </summary>
public static class StratagemStrength
{
    /// <summary>시전자 지력 vs 대상 지력의 강도 배율(퍼센트, 100 = 등호).</summary>
    public static int Percent(int casterIntellect, int targetIntellect)
        => System.Math.Clamp(100 + (casterIntellect - targetIntellect), 30, 200);
}
