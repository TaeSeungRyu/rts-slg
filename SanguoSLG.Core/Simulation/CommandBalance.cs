namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 도시 명령 밸런스 상수(design-administration.md). 코어 경제(BalanceConfig)와 분리해
/// 명령 튜닝을 한곳에 모은다. 값은 data/command-balance.json에서 로드한다.
/// </summary>
public sealed record CommandBalance
{
    /// <summary>보좌 능력 반영 계수(%) — 유효 능력 = 주관 + 보좌 × 이 값.</summary>
    public int AssistCoefficientPercent { get; init; } = 50;

    /// <summary>출신지 보너스(%) — 장수가 자기 출신 지역 도시에서 명령할 때.</summary>
    public int HomeRegionBonusPercent { get; init; } = 20;

    /// <summary>공통 명령 기간(일). 모병·징병·훈련·세율.</summary>
    public int CommandDays { get; init; } = 7;

    /// <summary>모병 1명령 상한 = 인구 × 이 %(정치 100 완전 동원 기준). 실제 = 이 % × 동원율.</summary>
    public int RecruitPopCapPercent { get; init; } = 1;

    /// <summary>징병 1명령 상한 = 인구 × 이 %(정치 100 완전 동원 기준). 실제 = 이 % × 동원율.</summary>
    public int ConscriptPopCapPercent { get; init; } = 3;

    /// <summary>징병 치안 하락 = 병력 1000당 이 값.</summary>
    public int ConscriptSecurityDropPer1000 { get; init; } = 5;

    /// <summary>모병 병력의 초기 훈련도.</summary>
    public int RecruitTrainLevel { get; init; } = 50;

    /// <summary>훈련 상승량 = 유효 무력 ÷ 이 값(명령당).</summary>
    public int TrainMightDivisor { get; init; } = 10;

    /// <summary>도시 훈련 명령의 훈련도 상한.</summary>
    public int TrainCap { get; init; } = 100;

    /// <summary>건설 기간(일).</summary>
    public int BuildDays { get; init; } = 30;

    /// <summary>건설 전제 — 수행 장수 정치 &gt; 이 값.</summary>
    public int BuildPoliticsRequired { get; init; } = 70;

    /// <summary>성곽 등급별 시설 슬롯(논·밭·마을 합계 상한).</summary>
    public int BuildSlotsSmall { get; init; } = 3;
    public int BuildSlotsMedium { get; init; } = 6;
    public int BuildSlotsLarge { get; init; } = 9;

    /// <summary>출전 가능 최소 훈련도(징병 부대는 이 밑이면 투입 불가 — design-unit-state 모집).</summary>
    public int DeployMinTraining { get; init; } = 50;

    /// <summary>보급부대 최대 편성 병력(design-unit-state 1단계-보급).</summary>
    public int SupplyMaxTroops { get; init; } = 20000;

    /// <summary>병종 연구 기본 기간(일) — 지력이 높으면 단축된다.</summary>
    public int ResearchBaseDays { get; init; } = 30;

    /// <summary>병종 연구 비용 기본치(금) — 비용 = 이 값 × 다음단계, 급증 단계부터는 ×2^초과.</summary>
    public int ResearchCostBase { get; init; } = 200;

    /// <summary>이 단계를 넘는 연구부터 비용이 지수(×2)로 급증한다(고급 티어 부담 — 2026-08-17).</summary>
    public int ResearchCostSteepFrom { get; init; } = 7;

    /// <summary>병종 연구 최대 단계(design-combat 10단계).</summary>
    public int ResearchMaxLevel { get; init; } = 10;

    /// <summary>성벽 연구 비용(금) = 이 값 × 다음 단계(선형, 세력 전체 성벽을 올리므로 병종보다 비쌈).</summary>
    public int WallResearchCostPerLevel { get; init; } = 1000;

    /// <summary>성벽 연구 최대 단계(design-combat 5단계 = 0~4).</summary>
    public int WallResearchMaxLevel { get; init; } = 4;

    /// <summary>수리 명령 기간(일) — 시설·성벽 공통(design-administration "건물 수리").</summary>
    public int RepairDays { get; init; } = 15;

    /// <summary>성벽 수리 회복량(명령당 연구 최대치의 %).</summary>
    public int WallRepairPercent { get; init; } = 25;

    /// <summary>성벽 수리 공방 가산(%p) — 공방 있는 도시.</summary>
    public int WallRepairWorkshopBonus { get; init; } = 25;

    /// <summary>성벽 수리 비용(금) = 회복량 ÷ 이 값.</summary>
    public int WallRepairGoldDivisor { get; init; } = 5;

    /// <summary>시설 건설 비용(금).</summary>
    public int BuildCostPaddy { get; init; } = 300;
    public int BuildCostFarm { get; init; } = 200;
    public int BuildCostVillage { get; init; } = 400;
    public int BuildCostWorkshop { get; init; } = 800;
}
