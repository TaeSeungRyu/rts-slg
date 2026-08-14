namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 세계 시계 엔진(design-administration "시간 축"). 일 단위로 시간을 흘리며 주기 틱을 발화한다:
/// 매월 말(그 달 30일)에 세금이 **도시 금고**로 들어간다(금은 도시별 소유 — 2026-08-13 확정).
/// 처리·저장은 항상 id 오름차순 — 결정론(CLAUDE.md 규칙 4).
/// </summary>
public sealed class WorldEngine
{
    private readonly BalanceConfig _balance;

    public WorldEngine(BalanceConfig balance) => _balance = balance;

    /// <summary><paramref name="days"/>일을 진행한 새 상태를 반환한다.</summary>
    public GameState AdvanceDays(GameState state, int days)
    {
        for (var i = 0; i < days; i++)
        {
            state = AdvanceDay(state);
        }

        return state;
    }

    /// <summary>한 달(30일)을 진행한다 — 기존 월 턴과의 호환 편의.</summary>
    public GameState AdvanceMonth(GameState state) => AdvanceDays(state, GameState.DaysPerMonth);

    private GameState AdvanceDay(GameState state)
    {
        var next = state with
        {
            Day = state.Day + 1,
            Factions = state.Factions.OrderBy(f => f.Id.Value).ToList(),
            Cities = state.Cities.OrderBy(c => c.Id.Value).ToList(),
        };

        // 월말 틱: 이 날이 그 달의 30일이면 세금 징수(도시 금고로) + 인구 성장.
        if (next.DayOfMonth == GameState.DaysPerMonth)
        {
            next = next with
            {
                Cities = next.Cities
                    .Select(c => Grow(c.AddGold(_balance.MonthlyTaxPerCity)))
                    .ToList(),
            };
        }

        return next;
    }

    // 인구 성장(2026-08-13 확정): 매월 말 +성장률% × 치안/100 (내림), 성곽 등급별 최대치까지.
    // 치안 100 = +1%, 치안 50 = +0.5% — 징병 남발이 장기 성장을 갉는다.
    private City Grow(City city)
    {
        var delta = (long)city.Population * _balance.PopulationGrowthPercent * city.Security / 10_000;
        var grown = city.Population + (int)delta;
        return city with { Population = System.Math.Min(grown, PopulationMax(city.Castle)) };
    }

    private int PopulationMax(CastleSize castle) => castle switch
    {
        CastleSize.Large => _balance.PopulationMaxLarge,
        CastleSize.Medium => _balance.PopulationMaxMedium,
        _ => _balance.PopulationMaxSmall,
    };
}
