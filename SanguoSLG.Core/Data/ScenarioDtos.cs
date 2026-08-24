namespace SanguoSLG.Core.Data;

// JSON 역직렬화 전용 DTO. 도메인 타입(강타입 ID, HexCoord)은 JSON에 직접 노출하지 않고
// 여기서 원시 값으로 받은 뒤 ScenarioLoader가 도메인으로 매핑한다.
// snake_case 키는 JsonSerializerOptions의 명명 정책으로 처리한다.

internal sealed class FactionDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Ruler { get; init; }
    public int Gold { get; init; }
    public string Color { get; init; } = "#c02626";
}

internal sealed class CityDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int Q { get; init; }
    public int R { get; init; }
    public int Owner { get; init; }
    public int Provisions { get; init; }

    // 성곽 등급: "small"(기본) | "medium" | "large"
    public string Castle { get; init; } = "small";
    public int Gold { get; init; }
    public int Security { get; init; } = 100;
    public int Population { get; init; }
    public int Ore { get; init; }
    public int Horses { get; init; }
    public int Elephants { get; init; }
    public string Region { get; init; } = "";
    public int Paddies { get; init; }
    public int Farms { get; init; }
    public int Villages { get; init; }
    public bool Workshop { get; init; }
    public bool ProducesOre { get; init; }
    public bool ProducesHorses { get; init; }
    public bool ProducesElephants { get; init; }
    public int TaxRate { get; init; } = 20;
    public int? Governor { get; init; }
}

internal sealed class GeneralDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";

    // 병종 분류 6종 → 등급 문자열("F"~"SSS", "A+"). spec-general "병종별 통솔".
    public Dictionary<string, string> Aptitudes { get; init; } = new();
    public int Might { get; init; }
    public int Intellect { get; init; }
    public int Politics { get; init; }
    public string? BattleActive { get; init; }
    public List<GeneralSkillDto> BattlePassives { get; init; } = new();
    public List<GeneralSkillDto> AdminPassives { get; init; } = new();
    public int Birth { get; init; }
    public string Region { get; init; } = "";
    public string Desc { get; init; } = "";
    public int Loyalty { get; init; } = 100;
}

internal sealed class GeneralSkillDto
{
    public string Code { get; init; } = "";
    public int Tier { get; init; } = 1;
}

internal sealed class BalanceDto
{
    public int MonthlyTaxPerCity { get; init; }
    public int MultiTargetSecondaryPercent { get; init; } = 60;
    public int WoundedPercent { get; init; } = 70;
    public int PopulationMaxSmall { get; init; } = 100_000;
    public int PopulationMaxMedium { get; init; } = 250_000;
    public int PopulationMaxLarge { get; init; } = 500_000;
    public int PopulationGrowthPercent { get; init; } = 1;
    public int GoldBaseSmall { get; init; } = 100;
    public int GoldBaseMedium { get; init; } = 200;
    public int GoldBaseLarge { get; init; } = 300;
    public int ProvisionsBaseSmall { get; init; } = 500;
    public int ProvisionsBaseMedium { get; init; } = 1000;
    public int ProvisionsBaseLarge { get; init; } = 2000;
    public int PaddyProvisions { get; init; } = 300;
    public int FarmProvisions { get; init; } = 150;
    public int VillageGold { get; init; } = 50;
    public int OreOutputPerMonth { get; init; } = 500;
    public int HorsesOutputPerMonth { get; init; } = 100;
    public int ElephantsOutputPerMonth { get; init; } = 2;
    public int TaxRateBase { get; init; } = 20;
    public int TaxRateMax { get; init; } = 50;
    public int TaxMaxSecurityPenalty { get; init; } = 10;
    public int PopulationIncomeFloorPercent { get; init; } = 50;
    public int SecurityNaturalRecovery { get; init; } = 2;
    public int SecurityLowThreshold { get; init; } = 20;
    public int SecurityLowIncomePercent { get; init; } = 70;
    public int GovernorMinPolitics { get; init; } = 60;
    public int NoGovernorIncomePercent { get; init; } = 30;
    public int GovernorTaxAmplifyAt100 { get; init; } = 100;
    public int WallMaxSmall { get; init; } = 3000;
    public int WallMaxMedium { get; init; } = 6000;
    public int WallMaxLarge { get; init; } = 10000;
    public int GeneralSalaryPerMonth { get; init; } = 20;
    public int MarketOrePrice { get; init; } = 1;
    public int MarketHorsePrice { get; init; } = 6;
    public int MarketElephantPrice { get; init; } = 3000;
    public int MarketGrainPricePer100 { get; init; } = 25;
    public int MarketJitterPercent { get; init; } = 15;
    public List<int>? MarketSeasonalPercent { get; init; }
}

internal sealed class MapDto
{
    public int MinQ { get; init; }
    public int MaxQ { get; init; }
    public int MinR { get; init; }
    public int MaxR { get; init; }
    public TerrainDto? Terrain { get; init; }
    public List<FeatureDto> Features { get; init; } = new();
    public List<ConditionDto> Conditions { get; init; } = new();
}

internal sealed class FeatureDto
{
    // 지물 종류: "mountain_medium"
    public string Type { get; init; } = "";
    public int Q { get; init; }
    public int R { get; init; }
}

internal sealed class ConditionDto
{
    // 파괴 상태: "ruined" | "burning" (정상은 기록하지 않는다)
    public string State { get; init; } = "";
    public int Q { get; init; }
    public int R { get; init; }
}

internal sealed class TerrainDto
{
    // 한 글자 코드 → 지형 이름(예: "G" → "plains")
    public Dictionary<string, string> Legend { get; init; } = new();

    // rows[i]는 r = min_r + i 행, 각 문자는 q = min_q + j 열의 지형 코드
    public List<string> Rows { get; init; } = new();
}
