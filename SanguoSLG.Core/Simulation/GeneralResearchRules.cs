namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>일반연구 6종의 공용 단계·연구선·효과 계산 규칙.</summary>
public static class GeneralResearchRules
{
    public const int MaxLevel = 10;
    public const int DefaultLowSecurityThreshold = 70;

    public sealed record Definition(string Code, string Name, string Description);

    public static readonly IReadOnlyList<Definition> Definitions =
    [
        new(FactionResearch.PublicOrderCode, "민심 안정", "홀수 단계에는 저치안 산출 페널티 기준이 낮아지고, 짝수 단계에는 주간 치안이 증가합니다."),
        new(FactionResearch.AgricultureCode, "농정 개량", "도시의 주간 군량 생산량이 단계마다 2% 증가합니다."),
        new(FactionResearch.CommerceCode, "상업 진흥", "도시의 주간 금 생산량이 단계마다 2% 증가합니다."),
        new(FactionResearch.ConscriptionCode, "군역 정비", "병력 담당자의 주간 병력 생산량이 단계마다 2% 증가합니다."),
        new(FactionResearch.TrainingCode, "훈련 교범", "짝수 단계마다 훈련 담당자의 주간 훈련도 증가량이 1 오릅니다."),
        new(FactionResearch.MedicineCode, "의술 연구", "수성 중이 아닌 도시의 부상병 회복 속도가 단계마다 10% 증가합니다."),
    ];

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

    public static int EffectiveSecurityForOutputPenalty(int security, int publicOrderLevel)
        => security + DefaultLowSecurityThreshold - LowSecurityThreshold(publicOrderLevel);

    public static int ApplyPercent(int amount, int percent)
        => amount <= 0 ? amount : amount * percent / 100;

    public static int Cost(int nextLevel, CommandBalance balance)
        => CommandEfficiency.ResearchCost(System.Math.Clamp(nextLevel, 1, MaxLevel), balance);

    public static string Name(string code)
        => Definitions.FirstOrDefault(x => x.Code == code)?.Name ?? code;

    public static string NextEffectText(string code, int currentLevel)
    {
        var next = System.Math.Clamp(currentLevel + 1, 1, MaxLevel);
        return code switch
        {
            FactionResearch.PublicOrderCode when next % 2 == 1
                => $"저치안 페널티 기준 {LowSecurityThreshold(next)} 미만",
            FactionResearch.PublicOrderCode => $"주간 치안 +{SecurityWeeklyBonus(next)}",
            FactionResearch.AgricultureCode => $"군량 생산 +{next * 2}%",
            FactionResearch.CommerceCode => $"금 생산 +{next * 2}%",
            FactionResearch.ConscriptionCode => $"병력 생산 +{next * 2}%",
            FactionResearch.TrainingCode => next % 2 == 0
                ? $"주간 훈련도 +{TrainingWeeklyBonus(next)}"
                : "다음 짝수 단계의 훈련 보너스를 준비",
            FactionResearch.MedicineCode => $"부상병 회복 +{next * 10}%",
            _ => string.Empty,
        };
    }

    private static int ClampLevel(int level) => System.Math.Clamp(level, 0, MaxLevel);
}
