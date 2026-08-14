namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 세계 시계 엔진(design-administration "시간 축"). 일 단위로 시간을 흘리며 주기 틱을 발화한다.
/// 매월 말(그 달 30일): 수입(금·군량 = 성 규모 기본치 + 시설 가산, 도시 금고로 — 금은 도시별
/// 소유), 자원 산출(산출 도시만), 인구 성장(치안 비례). 처리·저장은 항상 id 오름차순 — 결정론.
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

        // 월말 틱(그 달 30일): 수입(금·군량 = 성 규모 기본치 + 시설 가산) + 자원 산출 + 인구 성장.
        if (next.DayOfMonth == GameState.DaysPerMonth)
        {
            next = next with
            {
                Cities = next.Cities.Select(c => TaxSecurity(Grow(Produce(Income(c))))).ToList(),
            };
        }

        return next;
    }

    // 수입(design-administration "시설 건설"·"세율"): 금 = 성 규모 기본치 + 마을 가산,
    // 군량 = 성 규모 기본치 + 논·밭 가산 — 여기에 **세율 배율(세율/기준 20%)**이 곱해진다.
    // 공방은 수입이 아니라 생산·연구 게이트(③).
    private City Income(City city)
    {
        var rate = System.Math.Clamp(city.TaxRate, 0, _balance.TaxRateMax);
        var gold = (GoldBase(city.Castle) + city.Villages * _balance.VillageGold)
            * rate / _balance.TaxRateBase;
        var provisions = (ProvisionsBase(city.Castle)
            + city.Paddies * _balance.PaddyProvisions
            + city.Farms * _balance.FarmProvisions)
            * rate / _balance.TaxRateBase;
        return city with { Gold = city.Gold + gold, Provisions = city.Provisions + provisions };
    }

    // 세율의 민심(치안 통합 — 2026-08-13 확정) 반영: 기준(20%)보다 낮으면 회복, 높으면 하락,
    // 최대치(50%)면 크게 하락. 성장(Grow)은 이번 달 치안 기준으로 먼저 계산된다.
    private City TaxSecurity(City city)
    {
        var rate = System.Math.Clamp(city.TaxRate, 0, _balance.TaxRateMax);
        var delta = rate >= _balance.TaxRateMax
            ? -_balance.TaxMaxSecurityPenalty
            : (_balance.TaxRateBase - rate) / 5;
        return city with { Security = System.Math.Clamp(city.Security + delta, 0, 100) };
    }

    // 자원 산출: 산출 도시(지역 특산 플래그)만 매월 비축이 는다.
    private City Produce(City city) => city with
    {
        Ore = city.Ore + (city.ProducesOre ? _balance.OreOutputPerMonth : 0),
        Horses = city.Horses + (city.ProducesHorses ? _balance.HorsesOutputPerMonth : 0),
        Elephants = city.Elephants + (city.ProducesElephants ? _balance.ElephantsOutputPerMonth : 0),
    };

    private int GoldBase(CastleSize castle) => castle switch
    {
        CastleSize.Large => _balance.GoldBaseLarge,
        CastleSize.Medium => _balance.GoldBaseMedium,
        _ => _balance.GoldBaseSmall,
    };

    private int ProvisionsBase(CastleSize castle) => castle switch
    {
        CastleSize.Large => _balance.ProvisionsBaseLarge,
        CastleSize.Medium => _balance.ProvisionsBaseMedium,
        _ => _balance.ProvisionsBaseSmall,
    };

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
