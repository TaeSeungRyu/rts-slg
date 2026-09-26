namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>일반연구 6종의 공용 단계·연구선·효과 계산 규칙.</summary>
public static class GeneralResearchRules
{
    public const int MaxLevel = 10;
    public const int DefaultLowSecurityThreshold = 70;

    public static bool IsGeneralLane(string researchCode)
        => FactionResearch.IsGeneralResearch(researchCode);

    /// <summary>
    /// 민심 안정 홀수 단계는 저치안 산출 페널티 시작점을 2씩 낮춘다.
    /// Lv.1/2=68, Lv.3/4=66 … Lv.9/10=60.
    /// </summary>
    public static int LowSecurityThreshold(int level)
        => DefaultLowSecurityThreshold - 2 * ((ClampLevel(level) + 1) / 2);

    public static int SecurityWeeklyBonus(int level) => ClampLevel(level) / 2;
    public static int OutputPercent(int level) => 100 + 2 * ClampLevel(level);
    public static int TrainingWeeklyBonus(int level) => ClampLevel(level) / 2;
    public static int WoundedRecoveryPercent(int level) => 100 + 10 * ClampLevel(level);

    private static int ClampLevel(int level) => System.Math.Clamp(level, 0, MaxLevel);
}
