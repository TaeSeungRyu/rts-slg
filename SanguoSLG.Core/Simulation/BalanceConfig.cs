namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 밸런스 상수 묶음. 코드에 매직 넘버로 박지 않고 data/balance.json에서 주입한다.
/// 스켈레톤 단계의 값은 파이프 검증용 임시값이며, 실제 밸런스는 이후 설계에서 정한다.
/// </summary>
/// <param name="MonthlyTaxPerCity">도시당 월 세수(스켈레톤 임시값).</param>
/// <param name="MultiTargetSecondaryPercent">야전 다대일에서 주대상 외 대상 배수(design-combat.md 60%).</param>
/// <param name="WoundedPercent">피해 중 부상(회복 가능)으로 전환되는 비율(design-combat.md 70%, 나머지 30% 소실).</param>
/// <param name="GeneralSalaryPerMonth">장수 1인당 월 급여(금) — 미지급 시 충성 하락. 경제에 가벼운 상시 지출(design-general-lifecycle §1).</param>
public sealed record BalanceConfig(
    int MonthlyTaxPerCity,
    int MultiTargetSecondaryPercent = 60,
    int WoundedPercent = 70,
    int PopulationMaxSmall = 100_000,
    int PopulationMaxMedium = 250_000,
    int PopulationMaxLarge = 500_000,
    int PopulationGrowthPercent = 1,
    int GoldBaseSmall = 100,
    int GoldBaseMedium = 200,
    int GoldBaseLarge = 300,
    int ProvisionsBaseSmall = 500,
    int ProvisionsBaseMedium = 1000,
    int ProvisionsBaseLarge = 2000,
    int PaddyProvisions = 300,
    int FarmProvisions = 150,
    int VillageGold = 50,
    int OreOutputPerMonth = 500,
    int HorsesOutputPerMonth = 100,
    int ElephantsOutputPerMonth = 2,
    int TaxRateBase = 20,
    int TaxRateMax = 50,
    int TaxMaxSecurityPenalty = 10,
    int PopulationIncomeFloorPercent = 50,
    int SecurityNaturalRecovery = 2,
    int SecurityLowThreshold = 20,
    int SecurityLowIncomePercent = 70,
    int GovernorMinPolitics = 60,
    int NoGovernorIncomePercent = 30,
    int GovernorTaxAmplifyAt100 = 100,
    int WallMaxSmall = 3000,
    int WallMaxMedium = 6000,
    int WallMaxLarge = 10000,
    int GeneralSalaryPerMonth = 20,
    int ProvisionsPer10kPerDay = 10,
    int MarketOrePrice = 1,
    int MarketHorsePrice = 6,
    int MarketElephantPrice = 3000,
    int MarketGrainPricePer100 = 25,
    int MarketJitterPercent = 15,
    IReadOnlyList<int>? MarketSeasonalPercent = null)
{
    /// <summary>월별 시장 시세 배수(%). 9·10월(추수) 최저, 겨울 최고. 미지정 시 기본 계절 곡선.</summary>
    public int SeasonalPercent(int month)
    {
        var table = MarketSeasonalPercent is { Count: 12 }
            ? MarketSeasonalPercent
            : new[] { 140, 135, 115, 110, 105, 100, 100, 95, 70, 70, 95, 135 };
        return table[System.Math.Clamp(month, 1, 12) - 1];
    }
}
