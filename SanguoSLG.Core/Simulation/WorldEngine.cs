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

    private readonly List<WorldEvent> _events = new();

    /// <summary>직전 <see cref="AdvanceDays"/> 호출 동안 일어난 내정/라이프사이클 사건(표현 계층 보고용).</summary>
    public IReadOnlyList<WorldEvent> LastEvents => _events;

    /// <summary><paramref name="days"/>일을 진행한 새 상태를 반환한다.</summary>
    public GameState AdvanceDays(GameState state, int days)
    {
        _events.Clear();
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

        if (_commands.AutoOfficerSystemEnabled && next.DayOfMonth % 7 == 0)
        {
            var byId = next.Generals.ToDictionary(g => g.Id);
            next = ApplyAutoRecruitment(next, byId);
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
                Cities = next.Cities.Select(c => TaxSecurity(Grow(Produce(Income(next, c, Gov(c)), Gov(c))), Gov(c))).ToList(),
            };
            if (_commands.AutoOfficerSystemEnabled)
            {
                next = ApplyAutoOfficers(next, byId);
            }

            // 시장 시세 갱신(design-administration "시장"): 계절 배수 × 랜덤 지터(seeded — 결정론).
            // 9·10월(추수) 최저, 겨울 최고. 다음 달 매입가에 반영된다.
            var jitter = _balance.MarketJitterPercent;
            var draw = jitter > 0 ? _random.Next(-jitter, jitter + 1) : 0;
            var index = _balance.SeasonalPercent(next.Month) * (100 + draw) / 100;
            next = next with { MarketPricePercent = System.Math.Max(1, index) };

        }

        return next;
    }

    private GameState ApplyAutoOfficers(GameState state, IReadOnlyDictionary<GeneralId, Domain.General> byId)
    {
        var garrisons = state.Garrisons.ToList();
        var cities = new List<City>();
        foreach (var city in state.Cities)
        {
            var next = city;
            var security = ValidOfficer(state, city, city.SecurityOfficer, byId);
            var domestic = ValidOfficer(state, city, city.DomesticOfficer, byId);
            var recruiter = ValidOfficer(state, city, city.RecruitmentOfficer, byId);
            var trainer = ValidOfficer(state, city, city.TrainingOfficer, byId);

            var securityDelta = security is null ? _commands.AutoSecurityNoOfficerDelta : MightTier(security.Might);
            next = next with { Security = System.Math.Clamp(next.Security + securityDelta, 0, 100) };

            if (domestic is not null)
            {
                next = next with
                {
                    Gold = next.Gold + _commands.AutoDomesticGoldBase
                        + domestic.Politics * _commands.AutoDomesticGoldPoliticsMultiplier,
                    Provisions = next.Provisions + _commands.AutoDomesticProvisionsBase
                        + domestic.Politics * _commands.AutoDomesticProvisionsPoliticsMultiplier,
                };
            }

            if (trainer is not null)
            {
                var gain = System.Math.Max(1, MightTier(trainer.Might) + 1);
                garrisons = garrisons.Select(g => g.City == next.Id
                    ? g with { TrainingLevel = System.Math.Min(_commands.TrainCap, g.TrainingLevel + gain) }
                    : g).ToList();
            }

            cities.Add(next);
        }

        return state with { Cities = cities, GarrisonForces = garrisons };
    }

    private GameState ApplyAutoRecruitment(GameState state, IReadOnlyDictionary<GeneralId, Domain.General> byId)
    {
        var garrisons = state.Garrisons.ToList();
        var cities = new List<City>();
        foreach (var city in state.Cities)
        {
            var next = city;
            var recruiter = ValidOfficer(state, city, city.RecruitmentOfficer, byId);
            if (recruiter is not null)
            {
                var troopCodes = SelectedAutoRecruitTroopCodes(next).OrderBy(_commands.AutoRecruitGoldCostPer100)
                    .ThenBy(c => c, System.StringComparer.Ordinal).ToList();
                var totalTroops = _commands.AutoRecruitTroopsBase + recruiter.Might * _commands.AutoRecruitTroopsMightMultiplier;
                for (var i = 0; i < troopCodes.Count; i++)
                {
                    var code = troopCodes[i];
                    var troops = totalTroops / troopCodes.Count + (i < totalTroops % troopCodes.Count ? 1 : 0);
                    var cost = _commands.AutoRecruitGoldCost(code, troops);
                    if (cost <= 0 || next.Gold < cost)
                    {
                        continue;
                    }

                    next = next with { Gold = next.Gold - cost };
                    MergeGarrison(garrisons, next.Id, code, troops, _commands.AutoRecruitTroopTrainingLevel);
                }
            }

            cities.Add(next);
        }

        return state with { Cities = cities, GarrisonForces = garrisons };
    }

    private IEnumerable<string> SelectedAutoRecruitTroopCodes(City city)
    {
        var codes = CommandService.AutoRecruitTroopCodes(city.AutoRecruitTroopCodes).ToList();
        if (codes.Count > 0)
        {
            return codes;
        }

        return string.IsNullOrWhiteSpace(city.AutoRecruitTroopCode)
            ? [_commands.AutoRecruitDefaultTroopCode]
            : [city.AutoRecruitTroopCode];
    }

    private Domain.General? ValidOfficer(GameState state, City city, GeneralId? id,
        IReadOnlyDictionary<GeneralId, Domain.General> byId)
    {
        if (id is not { } gid || !byId.TryGetValue(gid, out var general))
        {
            return null;
        }

        if (state.Assignments.Count > 0)
        {
            var posting = state.PostingOf(gid);
            if (posting is null || posting.Location != city.Id || posting.Faction != city.Owner)
            {
                return null;
            }
        }

        return general;
    }

    private static int MightTier(int might) => might switch
    {
        < 60 => 0,
        < 80 => 1,
        < 100 => 2,
        _ => 3,
    };

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
        var postings = state.Assignments.ToList();
        var prisoners = state.Prisoners.ToList();
        var armies = state.Armies.ToList();
        var placements = state.Placements.ToList();

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
                    MergeGarrison(garrisons, cmd.City, cmd.TroopCode, cmd.Amount, trainingLevel: 0, trainee: true);
                    var drop = cmd.Amount / 1000 * _commands.ConscriptSecurityDropPer1000;
                    cities[cmd.City] = city with { Security = System.Math.Clamp(city.Security - drop, 0, 100) };
                    break;

                case CommandKind.Train:
                    var idx = garrisons.FindIndex(g => g.City == cmd.City && g.TroopCode == cmd.TroopCode
                        && g.Trainee == cmd.TraineePool);
                    if (idx >= 0)
                    {
                        var g = garrisons[idx];
                        var raised = g with
                        {
                            TrainingLevel = System.Math.Min(_commands.TrainCap, g.TrainingLevel + cmd.Amount),
                        };
                        // 신병 풀이 50에 도달하면 정규 풀로 자동 승격(가중 평균 — design-unit-state "신병 풀 분리").
                        if (raised.Trainee && raised.TrainingLevel >= 50)
                        {
                            garrisons.RemoveAt(idx);
                            MergeGarrison(garrisons, cmd.City, cmd.TroopCode, raised.Troops, raised.TrainingLevel);
                        }
                        else
                        {
                            garrisons[idx] = raised;
                        }
                    }

                    break;

                case CommandKind.Build:
                    // 완료 시 공사 인력(발행 때 인구에서 뗀 BuildSiteHp)을 인구로 되돌린다.
                    // (적에게 공사장이 파괴되면 그 인력은 전멸해 돌아오지 않는다 — 건설 취소 경로.)
                    cities[cmd.City] = Build(city, cmd.Facility)
                        with { Population = city.Population + _commands.BuildSiteHp };
                    // 사용자가 지정한 타일에 배치 기록(표현 계층이 그 자리에 모델을 얹는다). append-only.
                    if (cmd.Plot is { } builtPlot)
                    {
                        placements.Add(new FacilityPlacement(cmd.City, builtPlot, cmd.Facility, FacilityHealth.Level1));
                    }

                    break;

                case CommandKind.Upgrade:
                    UpgradePlacement(placements, cmd);
                    break;

                case CommandKind.SetTaxRate:
                    cities[cmd.City] = city with { TaxRate = cmd.Amount };
                    break;

                case CommandKind.Research:
                    if (cmd.TroopCode == FactionResearch.WallCode)
                    {
                        var wallLevel = System.Math.Clamp(cmd.Amount, 0, _commands.WallResearchMaxLevel);
                        cities[cmd.City] = city with
                        {
                            WallLevel = wallLevel,
                            Wall = CastleWall.Max(city.Castle, _balance, wallLevel),
                        };
                    }
                    else
                    {
                        var maxLevel = state.IsMajorTroop(city.Owner, cmd.TroopCode) ? _commands.ResearchMaxLevel : 7;
                        ResearchUp(research, city.Owner, cmd.TroopCode, maxLevel);
                    }

                    break;

                case CommandKind.Repair:
                    // 성벽 수리 — 예약 시 산출한 회복량(Amount)을 더하되 현 최대치를 넘지 않는다.
                    if (cmd.TroopCode == FactionResearch.WallCode)
                    {
                        var maxWall = CastleWall.Max(city.Castle, _balance, city.WallLevel);
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
                case CommandKind.Enlist:
                    ResolveEnlist(cmd, city, generals, postings, armies);
                    break;
            }

            // 명령 완료 사건(표현 계층 보고용) — 모병·징병·훈련·건설·연구·수리만.
            var evKind = cmd.Kind switch
            {
                CommandKind.Recruit => (WorldEventKind?)WorldEventKind.Recruit,
                CommandKind.Conscript => WorldEventKind.Conscript,
                CommandKind.Train => WorldEventKind.Train,
                CommandKind.Build => WorldEventKind.Build,
                CommandKind.Upgrade => WorldEventKind.Build,
                CommandKind.Research => WorldEventKind.Research,
                CommandKind.Repair => WorldEventKind.Repair,
                _ => null,
            };
            if (evKind is { } ek)
            {
                _events.Add(new WorldEvent(ek, city.Owner, cmd.Main, cmd.City, cmd.Amount,
                    cmd.TroopCode.Length > 0 ? cmd.TroopCode : cmd.Facility));
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
            Postings = postings,
            Captives = prisoners,
            FieldArmies = armies,
            PendingCommands = state.Commands.Where(c => c.CompletionDay != state.Day).ToList(),
            FacilityPlacements = placements,
        };
    }

    // 등용 정산: 완료 시점에 대상 종류를 다시 확인하고 수행 장수 정치 단일 확률로 판정.
    // 성공: 성 장수는 수행 도시 주둔, 출전중 장수는 부대째 전향. 실패 시 추가 페널티는 없다.
    private void ResolveEnlist(CityCommand cmd, City casterCity, List<Domain.General> generals,
        List<Domain.GeneralPosting> postings, List<CombatUnit> armies)
    {
        if (cmd.TargetGeneral is not { } targetId)
        {
            return;
        }

        var recruiter = generals.FirstOrDefault(g => g.Id == cmd.Main);
        var target = generals.FirstOrDefault(g => g.Id == targetId);
        if (recruiter is null || target is null)
        {
            return;
        }

        var faction = casterCity.Owner;
        var army = armies.FirstOrDefault(u => u.Field.Owner != faction && (u.VanguardId == targetId || u.AdjutantId == targetId));
        var cityPosting = army is null
            ? postings.FirstOrDefault(p => p.General == targetId && p.Faction != faction && p.Location is not null)
            : null;
        if (army is null && cityPosting is null)
        {
            return; // 대상이 사라졌거나 이미 아군
        }

        var success = _random.Next(0, 100) < EnlistOdds.SuccessPercent(recruiter.Politics);

        if (success)
        {
            if (army is not null)
            {
                var ai = armies.IndexOf(army);
                armies[ai] = army with { Field = army.Field with { Owner = faction } };
                SetPosting(postings, targetId, faction, null);
                if (army.VanguardId is { } v) { SetPosting(postings, v, faction, null); }
                if (army.AdjutantId is { } a) { SetPosting(postings, a, faction, null); }
            }
            else
            {
                SetPosting(postings, targetId, faction, casterCity.Id);
            }

            _events.Add(new WorldEvent(WorldEventKind.EnlistSuccess, faction, targetId, casterCity.Id));
            return;
        }

        _events.Add(new WorldEvent(WorldEventKind.EnlistFail, faction, targetId, casterCity.Id));
    }

    private static void SetPosting(List<Domain.GeneralPosting> postings, Domain.GeneralId g,
        Domain.FactionId faction, Domain.CityId? location)
    {
        var i = postings.FindIndex(p => p.General == g);
        var np = new Domain.GeneralPosting(g, faction, location);
        if (i >= 0) { postings[i] = np; } else { postings.Add(np); }
    }

    // 도시 계략 정산(design-stratagem "수행 규칙"): 지력 확률 성공 판정(시드 난수) → 실패 = 무효.
    // 대상이 그 사이 아군이 됐으면(함락 등) 캔슬. 효과는 종류별(성벽·치안·정찰·군량·금).
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
                var maxWall = CastleWall.Max(target.Castle, _balance, target.WallLevel);
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

    private static void UpgradePlacement(List<FacilityPlacement> placements, CityCommand cmd)
    {
        if (cmd.Plot is not { } plot)
        {
            return;
        }

        var idx = placements.FindIndex(p => p.City == cmd.City && p.Plot == plot);
        if (idx >= 0)
        {
            placements[idx] = placements[idx] with { HitPoints = cmd.Amount };
        }
    }

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
        int troops, int trainingLevel, bool trainee = false)
    {
        if (troops <= 0)
        {
            return;
        }

        var idx = garrisons.FindIndex(g => g.City == city && g.TroopCode == troopCode && g.Trainee == trainee);
        if (idx >= 0)
        {
            garrisons[idx] = garrisons[idx].Merge(troops, trainingLevel);
        }
        else
        {
            garrisons.Add(new GarrisonForce(city, troopCode, troops, trainingLevel, trainee));
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
    private City Income(GameState state, City city, Domain.General? governor)
    {
        var goldBase = GoldBase(city.Castle) + FacilityOutput(state, city, "village", city.Villages, _balance.VillageGold);
        var provBase = ProvisionsBase(city.Castle)
            + FacilityOutput(state, city, "paddy", city.Paddies, _balance.PaddyProvisions)
            + FacilityOutput(state, city, "farm", city.Farms, _balance.FarmProvisions);

        // 담당관(태수) 없거나 정치 미달이면 도시 경제가 무척 낮게 돌아간다(사용자 확정 2026-08-16).
        var effective = governor is not null && governor.Politics >= _balance.GovernorMinPolitics;

        // 내정 스킬 버킷(상재→금, 둔전→군량)은 유효 담당관일 때만.
        var goldBucket = effective ? GovernorBucket(governor, "tax") : 0;
        var provBucket = effective ? GovernorBucket(governor, "harvest") : 0;

        var gold = Scale(goldBase, city, effective, governor, goldBucket);
        var provisions = Scale(provBase, city, effective, governor, provBucket);
        return city with { Gold = city.Gold + gold, Provisions = city.Provisions + provisions };
    }

    private static int FacilityOutput(GameState state, City city, string code, int intactCount, int baseOutput)
    {
        var placements = state.Placements
            .Where(p => p.City == city.Id && p.Code == code)
            .OrderByDescending(p => FacilityHealth.OutputMultiplier(p.HitPoints))
            .ThenByDescending(p => p.HitPoints)
            .Take(intactCount)
            .ToList();
        var output = placements.Sum(p => baseOutput * FacilityHealth.OutputMultiplier(p.HitPoints));
        return output + System.Math.Max(0, intactCount - placements.Count) * baseOutput;
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
        => AdminBonus.Bucket(governor, _adminSkills, bucket);

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
