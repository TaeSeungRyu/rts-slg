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

    /// <summary>건설 배치 가능 반경 — 성 중심에서 이 거리 이내 타일에만 시설을 놓을 수 있다(표현 계층은
    /// 여기에 더해 평지·숲만 허용). 성 타일 자체·이미 놓인 칸은 제외한다.</summary>
    public int BuildPlotRadius { get; init; } = 2;

    /// <summary>공사장 체력(2026-08-27) — 공사 중 시설은 병력 1000짜리 무방비 목표로 취급한다.
    /// 아군·적군 가리지 않고 인접 부대가 매 진행 공격하고(공사는 반격 없음), 다 깎이면 건설이 취소된다.</summary>
    public int BuildSiteHp { get; init; } = 1000;

    /// <summary>공사장이 인접 부대 하나에게 매 진행 받는 피해. 여러 부대면 합산.</summary>
    public int BuildSiteDamagePerTurn { get; init; } = 500;

    /// <summary>성곽 등급별 시설 슬롯(논·밭·마을 합계 상한).</summary>
    public int BuildSlotsSmall { get; init; } = 3;
    public int BuildSlotsMedium { get; init; } = 6;
    public int BuildSlotsLarge { get; init; } = 9;

    /// <summary>출전 가능 최소 훈련도(징병 부대는 이 밑이면 투입 불가 — design-unit-state 모집).</summary>
    public int DeployMinTraining { get; init; } = 50;

    /// <summary>보급부대 최대 편성 병력(design-unit-state 1단계-보급).</summary>
    public int SupplyMaxTroops { get; init; } = 20000;

    /// <summary>성 보급 반경(칸) — 아군 성 이 반경 안의 아군 야전 부대는 매 진행 성 비축에서 군량을
    /// 채운다(성문 앞 대기·수비 부대가 굶지 않도록). 0이면 성 보급 없음.</summary>
    public int CityResupplyRadius { get; init; } = 3;

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

    /// <summary>약탈 노획률(%) — 파괴한 시설 건설 비용의 이 %를 노획한다(design-administration "시설 파괴·약탈").</summary>
    public int PlunderPercent { get; init; } = 50;

    /// <summary>시설 수리 비용 = 건설 비용 × 이 %(재건보다 싸다 — design-administration "건물 수리").</summary>
    public int RepairCostPercent { get; init; } = 50;

    /// <summary>지역 고정 자원 시설(광산·목장·상원) 수리 정액(금) — 건설비가 없어 별도.</summary>
    public int ResourceFacilityRepairCost { get; init; } = 400;

    /// <summary>사절·첩자 이동 속도(칸/일, 기병 기준) — 원거리 명령 소요일 = 기본 + ⌈거리÷속도⌉×2(왕복).</summary>
    public int CourierSpeed { get; init; } = 3;

    /// <summary>도시 계략 성벽파괴 — 성벽 최대치의 이 %를 깎는다.</summary>
    public int StratagemWallBreakPercent { get; init; } = 10;

    /// <summary>도시 계략 선동 — 치안을 이 값만큼 깎는다.</summary>
    public int StratagemInciteSecurity { get; init; } = 10;

    /// <summary>도시 계략 방화 — 군량 비축의 이 %를 태운다.</summary>
    public int StratagemArsonPercent { get; init; } = 20;

    /// <summary>도시 계략 절취 — 금고의 이 %를 훔쳐 수행 도시에 예치한다.</summary>
    public int StratagemStealPercent { get; init; } = 20;

    /// <summary>도시 계략 이간 — 대상 도시 충성 최저 장수의 충성을 min~max 랜덤만큼 깎는다(정찰 전제).</summary>
    public int StratagemDiscordLoyaltyMin { get; init; } = 5;
    public int StratagemDiscordLoyaltyMax { get; init; } = 15;

    /// <summary>시설 건설 비용(금).</summary>
    public int BuildCostPaddy { get; init; } = 300;
    public int BuildCostFarm { get; init; } = 200;
    public int BuildCostVillage { get; init; } = 400;
    public int BuildCostWorkshop { get; init; } = 400;

    public bool AutoOfficerSystemEnabled { get; init; } = false;
    public int AutoSecurityNoOfficerDelta { get; init; } = -2;
    public int AutoDomesticGoldBase { get; init; } = 100;
    public int AutoDomesticGoldPoliticsMultiplier { get; init; } = 2;
    public int AutoDomesticProvisionsBase { get; init; } = 300;
    public int AutoDomesticProvisionsPoliticsMultiplier { get; init; } = 5;
    public int AutoRecruitTroopsBase { get; init; } = 200;
    public int AutoRecruitTroopsMightMultiplier { get; init; } = 5;
    public int AutoRecruitTroopTrainingLevel { get; init; } = 50;
    public string AutoRecruitDefaultTroopCode { get; init; } = "swordsman";
    public Dictionary<string, int> AutoRecruitGoldCostPer100ByTroop { get; init; } = new()
    {
        ["swordsman"] = 1,
        ["archer"] = 1,
        ["thunder_cart"] = 1,
        ["catapult"] = 3,
        ["siege_tower"] = 3,
        ["cavalry"] = 4,
        ["war_elephant"] = 6,
    };

    public int AutoRecruitGoldCostPer100(string troopCode)
        => AutoRecruitGoldCostPer100ByTroop.TryGetValue(troopCode, out var cost) ? cost : 0;

    public int AutoRecruitGoldCost(string troopCode, int troops)
    {
        var costPer100 = AutoRecruitGoldCostPer100(troopCode);
        return costPer100 <= 0 || troops <= 0 ? 0 : (troops * costPer100 + 99) / 100;
    }
}
