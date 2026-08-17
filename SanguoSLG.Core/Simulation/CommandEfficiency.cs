namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 명령 효율 계산(design-administration.md "명령 실행 공통 규칙"·"내정 심화"). 순수 함수 —
/// 주관·보좌 능력 합산(A), 출신지 보너스(B)를 정수 연산으로 결정론적으로 계산한다.
/// 능력치는 명령별로 다르다(모병·징병·건설·세율=정치, 훈련=무력).
/// </summary>
public static class CommandEfficiency
{
    /// <summary>이 명령의 효율을 정하는 능력치가 정치인가(아니면 무력).</summary>
    public static bool UsesPolitics(CommandKind kind) => kind != CommandKind.Train;

    /// <summary>
    /// 유효 능력 = 주관 능력 × 고향배율 + 보좌 능력 × 보좌계수 × 고향배율. (백분율 정수 연산)
    /// 고향배율: 장수 출신 지역 = 도시 지역이면 (100 + 보너스%), 아니면 100.
    /// </summary>
    public static int Effective(General main, General? assist, City city, CommandKind kind, CommandBalance b)
    {
        var politics = UsesPolitics(kind);

        var mainPart = Stat(main, politics) * HomePercent(main, city, b);
        var assistPart = assist is null
            ? 0
            : Stat(assist, politics) * HomePercent(assist, city, b) * b.AssistCoefficientPercent / 100;

        return (mainPart + assistPart) / 100;
    }

    /// <summary>
    /// 모병·징병 산출 병력(자원 캡 적용 전) = 인구 × 상한% × **동원율**. 동원율 = 유효 정치/100
    /// (100에서 완전 동원 — 보좌·고향은 100까지 끌어올릴 뿐 넘지 못한다). 정치가 전 구간에서
    /// 선형으로 병력에 반영되고, 큰 도시일수록 절대 산출이 커진다(2026-08-17 개선).
    /// </summary>
    public static int RecruitTroops(int population, int capPercent, int effectivePolitics)
    {
        var mobilization = System.Math.Clamp(effectivePolitics, 0, 100);
        return (int)((long)population * capPercent / 100 * mobilization / 100);
    }

    /// <summary>훈련 상승량 = 유효 무력 ÷ 나눔값(최소 1).</summary>
    public static int TrainGain(int effectiveMight, CommandBalance b)
        => System.Math.Max(1, effectiveMight / b.TrainMightDivisor);

    /// <summary>
    /// 병종 연구 비용(금) — 목표 단계 <paramref name="nextLevel"/>로 올리는 값. 기본은 base×단계지만,
    /// 급증 시작 단계를 넘으면 ×2^(초과 단계)로 지수 급증한다(8~10단계는 세력 전체 자금이 필요할 만큼
    /// 무겁다 — 2026-08-17 사용자 확정). 예(base 200·급증 7): 7=1400, 8=3200, 9=7200, 10=16000.
    /// </summary>
    public static int ResearchCost(int nextLevel, CommandBalance b)
    {
        var cost = b.ResearchCostBase * nextLevel;
        if (nextLevel > b.ResearchCostSteepFrom)
        {
            cost *= 1 << (nextLevel - b.ResearchCostSteepFrom);
        }

        return cost;
    }

    /// <summary>성곽 등급별 시설 슬롯 총량.</summary>
    public static int BuildSlots(CastleSize castle, CommandBalance b) => castle switch
    {
        CastleSize.Large => b.BuildSlotsLarge,
        CastleSize.Medium => b.BuildSlotsMedium,
        _ => b.BuildSlotsSmall,
    };

    private static int Stat(General g, bool politics) => politics ? g.Politics : g.Might;

    private static int HomePercent(General g, City city, CommandBalance b)
        => g.Region.Length > 0 && g.Region == city.Region ? 100 + b.HomeRegionBonusPercent : 100;
}
