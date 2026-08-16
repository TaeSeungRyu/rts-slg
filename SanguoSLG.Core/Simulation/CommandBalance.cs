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

    /// <summary>모병 산출 = 유효 정치 × 이 값(병력/명령).</summary>
    public int RecruitTroopsPerPolitics { get; init; } = 15;

    /// <summary>모병 1명령 상한 = 인구 × 이 %.</summary>
    public int RecruitPopCapPercent { get; init; } = 1;

    /// <summary>징병 1명령 상한 = 인구 × 이 %.</summary>
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

    /// <summary>시설 건설 비용(금).</summary>
    public int BuildCostPaddy { get; init; } = 300;
    public int BuildCostFarm { get; init; } = 200;
    public int BuildCostVillage { get; init; } = 400;
    public int BuildCostWorkshop { get; init; } = 800;
}
