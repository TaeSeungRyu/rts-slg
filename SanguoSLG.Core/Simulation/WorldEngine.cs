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
    private readonly CommandBalance _commands;

    public WorldEngine(BalanceConfig balance, CommandBalance? commands = null)
    {
        _balance = balance;
        _commands = commands ?? new CommandBalance();
    }

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

        // 명령 정산: 완료일에 도달한 명령의 효과를 적용하고 목록에서 뺀다(수행 장수 잠금 해제).
        if (next.Commands.Any(c => c.CompletionDay == next.Day))
        {
            next = ResolveCommands(next);
        }

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

    // 명령 정산(design-administration "명령 실행 공통 규칙"): 완료일 명령의 효과를 도시에 적용하고
    // 목록에서 뺀다. 도시 id 순으로 결정론. 발행 시 자원·금은 이미 예약(차감)됐으므로 여기선 산출만 반영.
    private GameState ResolveCommands(GameState state)
    {
        var due = state.Commands.Where(c => c.CompletionDay == state.Day)
            .OrderBy(c => c.City.Value).ToList();
        var cities = state.Cities.ToDictionary(c => c.Id);

        foreach (var cmd in due)
        {
            if (!cities.TryGetValue(cmd.City, out var city))
            {
                continue; // 도시가 사라졌으면(함락 등) 산출은 증발한다.
            }

            cities[cmd.City] = cmd.Kind switch
            {
                CommandKind.Recruit => city.AddTroops(cmd.Amount, _commands.RecruitTrainLevel),
                CommandKind.Conscript => Conscript(city, cmd.Amount),
                CommandKind.Train => city with
                {
                    TrainingLevel = System.Math.Min(_commands.TrainCap, city.TrainingLevel + cmd.Amount),
                },
                CommandKind.Build => Build(city, cmd.Facility),
                CommandKind.SetTaxRate => city with { TaxRate = cmd.Amount },
                _ => city,
            };
        }

        return state with
        {
            Cities = cities.Values.OrderBy(c => c.Id.Value).ToList(),
            PendingCommands = state.Commands.Where(c => c.CompletionDay != state.Day).ToList(),
        };
    }

    private City Conscript(City city, int troops)
    {
        var drop = troops / 1000 * _commands.ConscriptSecurityDropPer1000;
        return city.AddTroops(troops, 0) with
        {
            Security = System.Math.Clamp(city.Security - drop, 0, 100),
        };
    }

    private static City Build(City city, string facility) => facility switch
    {
        "paddy" => city with { Paddies = city.Paddies + 1 },
        "farm" => city with { Farms = city.Farms + 1 },
        "village" => city with { Villages = city.Villages + 1 },
        "workshop" => city with { Workshop = true },
        _ => city,
    };

    // 수입(design-administration "시설 건설"·"세율"·"내정 심화"): 금 = 성 규모 기본치 + 마을 가산,
    // 군량 = 성 규모 기본치 + 논·밭 가산. 여기에 세 배율이 곱해진다(모두 정수 %):
    //   ① 세율 배율(세율/기준 20%)  ② 인구 충원율 배율(바닥% ~ 100%)  ③ 저치안 페널티(<임계면 감액).
    // 공방은 수입이 아니라 생산·연구 게이트(③).
    private City Income(City city)
    {
        var goldBase = GoldBase(city.Castle) + city.Villages * _balance.VillageGold;
        var provBase = ProvisionsBase(city.Castle)
            + city.Paddies * _balance.PaddyProvisions
            + city.Farms * _balance.FarmProvisions;

        var gold = Scale(goldBase, city);
        var provisions = Scale(provBase, city);
        return city with { Gold = city.Gold + gold, Provisions = city.Provisions + provisions };
    }

    // 세율·인구 충원율·저치안 페널티를 순서대로 곱한다(정수). base가 충분히 커 절삭 영향은 작다.
    private int Scale(int baseAmount, City city)
    {
        var rate = System.Math.Clamp(city.TaxRate, 0, _balance.TaxRateMax);
        var amount = baseAmount * rate / _balance.TaxRateBase;      // ① 세율
        amount = amount * PopulationFillPercent(city) / 100;        // ② 인구 충원율
        if (city.Security < _balance.SecurityLowThreshold)          // ③ 저치안 페널티
        {
            amount = amount * _balance.SecurityLowIncomePercent / 100;
        }

        return amount;
    }

    // 인구 충원율 배율(%): 바닥% + (100 − 바닥%) × 인구/최대치. 가득 찬 도시=100%, 텅 빈 도시=바닥%.
    private int PopulationFillPercent(City city)
    {
        var max = PopulationMax(city.Castle);
        if (max <= 0)
        {
            return 100;
        }

        var floor = _balance.PopulationIncomeFloorPercent;
        var fill = System.Math.Min(city.Population, max);
        return floor + (100 - floor) * fill / max;
    }

    // 치안(민심): 자연 회복 + 세율 효과. 기준(20%)보다 세율이 낮으면 추가 회복, 높으면 하락,
    // 최대치(50%)면 크게 하락. 성장(Grow)은 이번 달 치안 기준으로 먼저 계산된다.
    private City TaxSecurity(City city)
    {
        var rate = System.Math.Clamp(city.TaxRate, 0, _balance.TaxRateMax);
        var taxDelta = rate >= _balance.TaxRateMax
            ? -_balance.TaxMaxSecurityPenalty
            : (_balance.TaxRateBase - rate) / 5;
        var delta = _balance.SecurityNaturalRecovery + taxDelta;
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
