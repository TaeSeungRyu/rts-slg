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
    private readonly IReadOnlyDictionary<string, Domain.AdminSkill> _adminSkills;

    public WorldEngine(BalanceConfig balance, CommandBalance? commands = null,
        IReadOnlyList<Domain.AdminSkill>? adminSkills = null, IRandomSource? random = null)
    {
        _balance = balance;
        _commands = commands ?? new CommandBalance();
        _adminSkills = (adminSkills ?? []).ToDictionary(a => a.Code);
        _random = random ?? new SeededRandomSource(0); // 도시 계략 성공 판정용(시드 — 결정론)
    }

    private readonly IRandomSource _random;

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
            var byId = next.Generals.ToDictionary(g => g.Id);

            // 담당관은 그 도시에 실제 주둔 중일 때만 유효 — 출전(Location null)하면 유령 태수가 되지
            // 않게 한다. 배속 데이터가 없으면(포커스 테스트) 주둔 검사를 생략한다.
            Domain.General? Gov(City c)
            {
                if (c.Governor is not { } gid || !byId.TryGetValue(gid, out var g))
                {
                    return null;
                }

                if (next.Assignments.Count > 0 && next.PostingOf(gid)?.Location != c.Id)
                {
                    return null;
                }

                return g;
            }
            next = next with
            {
                Cities = next.Cities.Select(c => TaxSecurity(Grow(Produce(Income(c, Gov(c)), Gov(c))), Gov(c))).ToList(),
            };
        }

        return next;
    }

    // 명령 정산(design-administration "명령 실행 공통 규칙"): 완료일 명령의 효과를 도시에 적용하고
    // 목록에서 뺀다. 도시 id 순으로 결정론. 발행 시 자원·금은 이미 예약(차감)됐으므로 여기선 산출만 반영.
    private GameState ResolveCommands(GameState state)
    {
        var due = state.Commands.Where(c => c.CompletionDay == state.Day)
            .OrderBy(c => c.City.Value).ThenBy(c => c.Main.Value).ToList();
        var cities = state.Cities.ToDictionary(c => c.Id);
        var garrisons = state.Garrisons.ToList();
        var research = state.Research.ToList();
        var generals = state.Generals.ToList();
        var intel = state.Intel.ToList();

        foreach (var cmd in due)
        {
            if (!cities.TryGetValue(cmd.City, out var city))
            {
                continue; // 도시가 사라졌으면(함락 등) 산출은 증발한다.
            }

            switch (cmd.Kind)
            {
                case CommandKind.Recruit:
                    MergeGarrison(garrisons, cmd.City, cmd.TroopCode, cmd.Amount, _commands.RecruitTrainLevel);
                    break;

                case CommandKind.Conscript:
                    MergeGarrison(garrisons, cmd.City, cmd.TroopCode, cmd.Amount, trainingLevel: 0);
                    var drop = cmd.Amount / 1000 * _commands.ConscriptSecurityDropPer1000;
                    cities[cmd.City] = city with { Security = System.Math.Clamp(city.Security - drop, 0, 100) };
                    break;

                case CommandKind.Train:
                    var idx = garrisons.FindIndex(g => g.City == cmd.City && g.TroopCode == cmd.TroopCode);
                    if (idx >= 0)
                    {
                        var g = garrisons[idx];
                        garrisons[idx] = g with
                        {
                            TrainingLevel = System.Math.Min(_commands.TrainCap, g.TrainingLevel + cmd.Amount),
                        };
                    }

                    break;

                case CommandKind.Build:
                    cities[cmd.City] = Build(city, cmd.Facility);
                    break;

                case CommandKind.SetTaxRate:
                    cities[cmd.City] = city with { TaxRate = cmd.Amount };
                    break;

                case CommandKind.Research:
                    // 공방 게이트 재확인 — 완료 시점에 공방을 잃었으면 연구 성과는 증발한다.
                    if (city.Workshop && cmd.TroopCode == FactionResearch.WallCode)
                    {
                        var wallLevel = ResearchUp(research, city.Owner, FactionResearch.WallCode, _commands.WallResearchMaxLevel);
                        // 성벽 연구 완료 → 그 세력 모든 도시 성벽을 새 최대치로(전면 증축).
                        foreach (var owned in cities.Values.Where(c => c.Owner == city.Owner).ToList())
                        {
                            cities[owned.Id] = owned with { Wall = CastleWall.Max(owned.Castle, _balance, wallLevel) };
                        }
                    }
                    else if (city.Workshop)
                    {
                        ResearchUp(research, city.Owner, cmd.TroopCode, _commands.ResearchMaxLevel);
                    }

                    break;

                case CommandKind.Repair:
                    // 성벽 수리 — 예약 시 산출한 회복량(Amount)을 더하되 현 최대치를 넘지 않는다.
                    if (cmd.TroopCode == FactionResearch.WallCode)
                    {
                        var maxWall = CastleWall.Max(city.Castle, _balance, state.WallLevelOf(city.Owner));
                        cities[cmd.City] = city with { Wall = System.Math.Min(maxWall, city.Wall + cmd.Amount) };
                    }
                    else
                    {
                        cities[cmd.City] = RepairFacility(city, cmd.Facility);
                    }

                    break;

                case CommandKind.CityStratagem:
                    ResolveCityStratagem(state, cmd, city, cities, generals, intel);
                    break;
            }
        }

        return state with
        {
            Cities = cities.Values.OrderBy(c => c.Id.Value).ToList(),
            GarrisonForces = garrisons
                .Where(g => g.Troops > 0)
                .OrderBy(g => g.City.Value).ThenBy(g => g.TroopCode, System.StringComparer.Ordinal)
                .ToList(),
            ResearchTracks = research
                .OrderBy(r => r.Faction.Value).ThenBy(r => r.TroopCode, System.StringComparer.Ordinal)
                .ToList(),
            Generals = generals,
            ScoutedCities = intel
                .OrderBy(i => i.Faction.Value).ThenBy(i => i.City.Value)
                .ToList(),
            PendingCommands = state.Commands.Where(c => c.CompletionDay != state.Day).ToList(),
        };
    }

    // 도시 계략 정산(design-stratagem "수행 규칙"): 지력 확률 성공 판정(시드 난수) → 실패 = 무효.
    // 대상이 그 사이 아군이 됐으면(함락 등) 캔슬. 효과는 종류별(성벽·치안·정찰·군량·금·충성).
    private void ResolveCityStratagem(GameState state, CityCommand cmd, City casterCity,
        Dictionary<CityId, City> cities, List<Domain.General> generals, List<Domain.CityIntel> intel)
    {
        if (cmd.TargetCity is not { } targetId || !cities.TryGetValue(targetId, out var target)
            || target.Owner == casterCity.Owner)
        {
            return;
        }

        var casterIntellect = generals.FirstOrDefault(g => g.Id == cmd.Main)?.Intellect ?? 40;
        var defenderIntellect = target.Governor is { } gov
            ? generals.FirstOrDefault(g => g.Id == gov)?.Intellect
            : null;
        var success = _random.Next(0, 100) < CityStratagems.SuccessPercent(casterIntellect, defenderIntellect);
        if (!success)
        {
            return; // 실패 = 무효(소요 기간·장수 잠금이 이미 비용)
        }

        switch (cmd.Facility)
        {
            case "wall_break":
                var maxWall = CastleWall.Max(target.Castle, _balance, state.WallLevelOf(target.Owner));
                cities[targetId] = target with { Wall = System.Math.Max(0, target.Wall - maxWall * _commands.StratagemWallBreakPercent / 100) };
                break;

            case "incite":
                cities[targetId] = target with { Security = System.Math.Clamp(target.Security - _commands.StratagemInciteSecurity, 0, 100) };
                break;

            case "scout":
                if (!intel.Any(i => i.Faction == casterCity.Owner && i.City == targetId))
                {
                    intel.Add(new Domain.CityIntel(casterCity.Owner, targetId));
                }

                break;

            case "arson":
                cities[targetId] = target with { Provisions = target.Provisions - target.Provisions * _commands.StratagemArsonPercent / 100 };
                break;

            case "steal":
                var stolen = target.Gold * _commands.StratagemStealPercent / 100;
                cities[targetId] = target with { Gold = target.Gold - stolen };
                cities[cmd.City] = cities[cmd.City].AddGold(stolen); // 수행 도시에 예치
                break;

            case "sow_discord":
                // 대상 도시 주둔 장수 중 충성 최저 1명(동률 id순) −N.
                var victim = state.Assignments
                    .Where(p => p.Location == targetId)
                    .Select(p => generals.FirstOrDefault(g => g.Id == p.General))
                    .OfType<Domain.General>()
                    .OrderBy(g => g.Loyalty).ThenBy(g => g.Id.Value)
                    .FirstOrDefault();
                if (victim is not null)
                {
                    var idx = generals.FindIndex(g => g.Id == victim.Id);
                    generals[idx] = victim with { Loyalty = System.Math.Max(0, victim.Loyalty - _commands.StratagemDiscordLoyalty) };
                }

                break;
        }
    }

    // 시설 수리 완료 — 잔해를 시설로 되돌리거나(일반), 파괴 플래그를 해제한다(자원 시설).
    private static City RepairFacility(City city, string facility) => facility switch
    {
        "paddy" when city.RuinedPaddies > 0 => city with { Paddies = city.Paddies + 1, RuinedPaddies = city.RuinedPaddies - 1 },
        "farm" when city.RuinedFarms > 0 => city with { Farms = city.Farms + 1, RuinedFarms = city.RuinedFarms - 1 },
        "village" when city.RuinedVillages > 0 => city with { Villages = city.Villages + 1, RuinedVillages = city.RuinedVillages - 1 },
        "workshop" when city.WorkshopRuined => city with { Workshop = true, WorkshopRuined = false },
        "mine" => city with { MineDestroyed = false },
        "ranch" => city with { RanchDestroyed = false },
        "elephant_garden" => city with { ElephantGardenDestroyed = false },
        _ => city,
    };

    // 세력 연구 트랙 +1(최대 캡). 없으면 새 트랙(1단계). 갱신된 단계를 돌려준다.
    private static int ResearchUp(List<FactionResearch> research, FactionId faction, string troopCode, int maxLevel)
    {
        var idx = research.FindIndex(r => r.Faction == faction && r.TroopCode == troopCode);
        if (idx >= 0)
        {
            var level = System.Math.Min(maxLevel, research[idx].Level + 1);
            research[idx] = research[idx] with { Level = level };
            return level;
        }

        research.Add(new FactionResearch(faction, troopCode, 1));
        return 1;
    }

    // 대기 병력 합류(같은 도시·병종이면 가중 평균 희석, 없으면 새 항목).
    private static void MergeGarrison(List<GarrisonForce> garrisons, CityId city, string troopCode,
        int troops, int trainingLevel)
    {
        if (troops <= 0)
        {
            return;
        }

        var idx = garrisons.FindIndex(g => g.City == city && g.TroopCode == troopCode);
        if (idx >= 0)
        {
            garrisons[idx] = garrisons[idx].Merge(troops, trainingLevel);
        }
        else
        {
            garrisons.Add(new GarrisonForce(city, troopCode, troops, trainingLevel));
        }
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
    private City Income(City city, Domain.General? governor)
    {
        var goldBase = GoldBase(city.Castle) + city.Villages * _balance.VillageGold;
        var provBase = ProvisionsBase(city.Castle)
            + city.Paddies * _balance.PaddyProvisions
            + city.Farms * _balance.FarmProvisions;

        // 담당관(태수) 없거나 정치 미달이면 도시 경제가 무척 낮게 돌아간다(사용자 확정 2026-08-16).
        var effective = governor is not null && governor.Politics >= _balance.GovernorMinPolitics;

        // 내정 스킬 버킷(상재→금, 둔전→군량)은 유효 담당관일 때만.
        var goldBucket = effective ? GovernorBucket(governor, "tax") : 0;
        var provBucket = effective ? GovernorBucket(governor, "harvest") : 0;

        var gold = Scale(goldBase, city, effective, governor, goldBucket);
        var provisions = Scale(provBase, city, effective, governor, provBucket);
        return city with { Gold = city.Gold + gold, Provisions = city.Provisions + provisions };
    }

    // 수입 = base × (스킬 버킷) × 세율배율 × 인구 충원율 × 저치안. 세율배율은 담당관에 따라 갈린다:
    //  · 유효 담당관: 정치가 세율을 증폭(정치 100 → 세율 효과 2배 — 10% 세율이 20%처럼, 치안은 실세율 기준).
    //  · 없거나 정치 미달: 세율배율에 무거운 페널티(no_governor_income_percent) — 경제가 무척 낮아진다.
    private int Scale(int baseAmount, City city, bool effectiveGovernor, Domain.General? governor, int bucketPercent)
    {
        var amount = baseAmount * (100 + bucketPercent) / 100;                 // 내정 스킬
        var rate = System.Math.Clamp(city.TaxRate, 0, _balance.TaxRateMax);

        if (effectiveGovernor)
        {
            var amplify = TaxAmplifyPercent(governor!);                        // 정치 세율 증폭
            var effectiveRate = rate * (100 + amplify) / 100;
            amount = amount * effectiveRate / _balance.TaxRateBase;            // ① 증폭 세율
        }
        else
        {
            amount = amount * rate / _balance.TaxRateBase;                     // ① 세율(증폭 없음)
            amount = amount * _balance.NoGovernorIncomePercent / 100;          // 담당관 없음 페널티
        }

        amount = amount * PopulationFillPercent(city) / 100;                   // ② 인구 충원율
        if (city.Security < _balance.SecurityLowThreshold)                     // ③ 저치안 페널티
        {
            amount = amount * _balance.SecurityLowIncomePercent / 100;
        }

        return amount;
    }

    // 정치 세율 증폭%: (정치 − 최소치) × 100정치기준값 ÷ (100 − 최소치). 정치 100 → +100%(2배), 최소치 → 0%.
    private int TaxAmplifyPercent(Domain.General governor)
    {
        var span = 100 - _balance.GovernorMinPolitics;
        if (span <= 0)
        {
            return 0;
        }

        return System.Math.Max(0, governor.Politics - _balance.GovernorMinPolitics)
            * _balance.GovernorTaxAmplifyAt100 / span;
    }

    // 담당관의 내정 패시브 스킬 중 해당 버킷의 티어값 합(상재=tax, 둔전=harvest, 진무=security).
    private int GovernorBucket(Domain.General? governor, string bucket)
    {
        if (governor is null)
        {
            return 0;
        }

        var sum = 0;
        foreach (var held in governor.AdminPassives ?? [])
        {
            if (_adminSkills.TryGetValue(held.Code, out var def) && def.Bucket == bucket)
            {
                sum += def.AmountAtTier(held.Tier);
            }
        }

        return sum;
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

    // 치안(민심): 자연 회복 + 세율 효과 + 유효 담당관의 진무 스킬 회복. 기준(20%)보다 세율이 낮으면
    // 추가 회복, 높으면 하락, 최대치(50%)면 크게 하락. 성장(Grow)은 이번 달 치안 기준으로 먼저 계산된다.
    private City TaxSecurity(City city, Domain.General? governor)
    {
        var rate = System.Math.Clamp(city.TaxRate, 0, _balance.TaxRateMax);
        var taxDelta = rate >= _balance.TaxRateMax
            ? -_balance.TaxMaxSecurityPenalty
            : (_balance.TaxRateBase - rate) / 5;
        var effective = governor is not null && governor.Politics >= _balance.GovernorMinPolitics;
        var pacify = effective ? GovernorBucket(governor, "security") / 10 : 0; // 진무 티어(10/20/30)→+1/2/3
        var delta = _balance.SecurityNaturalRecovery + taxDelta + pacify;
        return city with { Security = System.Math.Clamp(city.Security + delta, 0, 100) };
    }

    // 자원 산출: 산출 도시(지역 특산 플래그)만 매월 비축이 는다. 유효 담당관의 채광·목마·상사
    // 스킬이 있으면 해당 자원 산출량이 티어%만큼 증가한다(그 자원을 내지 않는 도시엔 효과 없음).
    private City Produce(City city, Domain.General? governor)
    {
        var effective = governor is not null && governor.Politics >= _balance.GovernorMinPolitics;
        int Output(int baseOutput, bool produces, string bucket)
        {
            if (!produces)
            {
                return 0;
            }

            var bonus = effective ? GovernorBucket(governor, bucket) : 0;
            return baseOutput * (100 + bonus) / 100;
        }

        // 자원 시설(광산·목장·상원)이 파괴됐으면 생산 중단 — 수리로 재개(design-administration).
        return city with
        {
            Ore = city.Ore + Output(_balance.OreOutputPerMonth, city.ProducesOre && !city.MineDestroyed, "ore_output"),
            Horses = city.Horses + Output(_balance.HorsesOutputPerMonth, city.ProducesHorses && !city.RanchDestroyed, "horse_output"),
            Elephants = city.Elephants + Output(_balance.ElephantsOutputPerMonth, city.ProducesElephants && !city.ElephantGardenDestroyed, "elephant_output"),
        };
    }

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
