namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 캠페인 진행(2026-08-16 확정): **진행 버튼 1번 = 7일 고정.** 야전(AdvanceOrchestrator)이
/// 접적으로 멈춰도 7일이 찰 때까지 자동 재개해 이동+전투가 계속되고(일 단위 교전 반복),
/// 내정(WorldEngine)도 같은 7일을 흐른다 — 두 세계가 한 시계를 쓴다. 판단 기회는 주 단위라
/// 한 번의 명령이 무겁다(design-movement "캠페인 해석").
/// 입성 부대는 도시 대기 병력(GarrisonForce)으로 편입된다.
/// </summary>
public sealed class CampaignEngine
{
    /// <summary>진행 1번의 길이(일) — 7일 고정(2026-08-16 확정).</summary>
    public const int WeekDays = 7;

    private readonly AdvanceOrchestrator _field;
    private readonly WorldEngine _world;
    private readonly CampaignSiege? _siege;
    private readonly CityCapture? _capture;
    private readonly CityPlunder? _plunder;
    private readonly IRandomSource _random;
    private readonly int _cityResupplyRadius;

    public CampaignEngine(AdvanceOrchestrator field, WorldEngine world,
        CampaignSiege? siege = null, CityCapture? capture = null, IRandomSource? random = null,
        CityPlunder? plunder = null, int cityResupplyRadius = 0)
    {
        _field = field;
        _world = world;
        _siege = siege;
        _capture = capture;
        _plunder = plunder;
        _random = random ?? new SeededRandomSource(0);
        _cityResupplyRadius = cityResupplyRadius;
    }

    /// <summary>7일을 진행한 새 상태를 반환한다. 야전 진행 보고 목록은 <paramref name="turns"/>로.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns)
        => AdvanceWeek(state, out turns, out _, out _);

    /// <summary>7일 진행 + 공성 교환 보고(<paramref name="sieges"/>)까지 돌려주는 오버로드.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns,
        out IReadOnlyList<SiegeExchange> sieges)
        => AdvanceWeek(state, out turns, out sieges, out _);

    /// <summary>7일 진행 + 공성 교환 + 함락 보고(<paramref name="captures"/>)까지 돌려주는 오버로드.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns,
        out IReadOnlyList<SiegeExchange> sieges, out IReadOnlyList<CaptureReport> captures)
        => AdvanceWeek(state, out turns, out sieges, out captures, out _);

    /// <summary>7일 진행 + 공성·함락·약탈 보고(<paramref name="plunders"/>)까지 돌려주는 오버로드.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns,
        out IReadOnlyList<SiegeExchange> sieges, out IReadOnlyList<CaptureReport> captures,
        out IReadOnlyList<PlunderReport> plunders)
        => AdvanceWeek(state, out turns, out sieges, out captures, out plunders, out _);

    /// <summary>야전 전멸 장수 처리 보고(<paramref name="casualties"/>)까지 — design-general-lifecycle §4b.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns,
        out IReadOnlyList<SiegeExchange> sieges, out IReadOnlyList<CaptureReport> captures,
        out IReadOnlyList<PlunderReport> plunders, out IReadOnlyList<CasualtyReport> casualties)
    {
        var reports = new List<AdvanceTurn>();
        var siegeReports = new List<SiegeExchange>();
        var captureReports = new List<CaptureReport>();
        var plunderReports = new List<PlunderReport>();
        var casualtyReports = new List<CasualtyReport>();
        var work = state;
        var armies = state.Armies.Where(u => u.Pool.Active > 0).ToList();

        var remaining = WeekDays;
        while (remaining > 0 && armies.Count > 0)
        {
            // 성 접적 정지용 장애물 — 매 진행마다 현재 소유로 갱신(함락으로 주인이 바뀌므로).
            var castles = work.Cities
                .OrderBy(c => c.Id.Value)
                .Select(c => new SiegeSite(c.Position, c.Owner))
                .ToList();
            var cityAt = work.Cities.ToDictionary(c => c.Position, c => c.Id);

            // 성 보급(2026-08-20): 이동 전, 아군 성 반경 안의 아군 야전 부대 군량을 성 비축에서 채운다
            //  — 성문 앞 대기·수비 부대가 굶지 않도록(보급부대와 같은 원리, 성이 고정 보급원).
            if (_cityResupplyRadius > 0)
            {
                (work, armies) = ResupplyFromCities(work, armies);
            }

            var turn = _field.Run(armies, maxDays: remaining, castles);
            reports.Add(turn);
            remaining -= System.Math.Max(1, turn.Movement.Days);

            // 야전 전멸 장수 처리(§4b): 이 조각에서 사라진 부대(입성 제외)의 선봉·부관 판정.
            // 교전 사망(피해 기록 있음)이면 최근접 적 부대의 세력이 포획 후보, 아니면 100% 탈출.
            var survivors = turn.Units.Select(u => u.Id).ToHashSet();
            var enteredNow = turn.EnteredCastle.Select(u => u.Id).ToHashSet();
            var movedPos = turn.Movement.Units.ToDictionary(f => f.Id, f => f.Position);
            foreach (var dead in armies
                .Where(u => !survivors.Contains(u.Id) && !enteredNow.Contains(u.Id))
                .OrderBy(u => u.Id.Value))
            {
                var at = movedPos.TryGetValue(dead.Id, out var mp) ? mp : dead.Field.Position;
                FactionId? captor = null;
                if (turn.Combat is { } cbt && cbt.DamageTaken.ContainsKey(dead.Id))
                {
                    captor = turn.Units
                        .Where(o => o.Field.Owner != dead.Field.Owner)
                        .OrderBy(o => o.Field.Position.Distance(at)).ThenBy(o => o.Id.Value)
                        .Select(o => (FactionId?)o.Field.Owner)
                        .FirstOrDefault();
                }

                work = FieldCasualties.ResolveUnit(work, dead, captor, at, _random, casualtyReports);
            }

            armies = turn.Units.ToList();

            work = ApplyEntered(work, turn.EnteredCastle, cityAt);

            // 공성 교환(design-combat "성 전투") — 접적으로 멈춘 공격 부대가 성벽·수비를 깎는다.
            if (_siege is not null)
            {
                var siegeState = work;
                var result = _siege.Resolve(armies, siegeState.Cities, siegeState.Garrisons, CounterAptitude);

                // 성 반격 위력 = 유효 태수(그 도시에 실제 주둔한 소속 장수) 무력의 위력 배수. 없으면 100%.
                int CounterAptitude(CityId cid)
                {
                    var cc = siegeState.Cities.FirstOrDefault(c => c.Id == cid);
                    if (cc?.Governor is not { } gid)
                    {
                        return 100;
                    }

                    var posting = siegeState.PostingOf(gid);
                    if (posting is null || posting.Location != cid || posting.Faction != cc.Owner)
                    {
                        return 100;
                    }

                    var gov = siegeState.Generals.FirstOrDefault(g => g.Id == gid);
                    return gov is null ? 100 : StatScale.Percent(gov.Might);
                }

                // 성 반격으로 전멸한 공성 부대의 장수 판정(§4b) — 포획 후보 = 그 성의 소유 세력.
                foreach (var dead in result.Armies.Where(u => u.Pool.Active <= 0).OrderBy(u => u.Id.Value))
                {
                    var ex = result.Exchanges.FirstOrDefault(e => e.Besiegers.Contains(dead.Id));
                    FactionId? captor = ex is null ? null : work.Cities.First(c => c.Id == ex.City).Owner;
                    work = FieldCasualties.ResolveUnit(work, dead, captor, dead.Field.Position, _random, casualtyReports);
                }

                armies = result.Armies.Where(u => u.Pool.Active > 0).ToList();
                work = work with { Cities = result.Cities, GarrisonForces = result.Garrisons };
                // 어느 진행 조각의 공성인지 스탬프 — 표현 계층의 재생 타이밍용.
                siegeReports.AddRange(result.Exchanges.Select(e => e with { TurnIndex = reports.Count - 1 }));
            }

            // 약탈(design-administration "시설 파괴·약탈") — 포위군이 진행마다 시설 1개 파괴·노획.
            if (_plunder is not null)
            {
                var looted = _plunder.Resolve(armies, work.Cities);
                armies = looted.Armies.ToList();
                work = work with { Cities = looted.Cities };
                plunderReports.AddRange(looted.Reports);
            }

            // 함락 처리(design-general-lifecycle §4) — 성벽0+수비0에 근접 공격군이 있으면 점거.
            if (_capture is not null)
            {
                work = _capture.ResolveAll(work with { FieldArmies = armies }, _random, out var caps);
                armies = work.Armies.Where(u => u.Pool.Active > 0).ToList();
                captureReports.AddRange(caps);
            }
        }

        var afterField = work with
        {
            FieldArmies = armies,
            GarrisonForces = work.Garrisons
                .Where(g => g.Troops > 0)
                .OrderBy(g => g.City.Value).ThenBy(g => g.TroopCode, System.StringComparer.Ordinal).ThenBy(g => g.Trainee)
                .ToList(),
        };

        // 포로 충성 하락(일주일 −1 — design-general-lifecycle §2): 억류될수록 등용이 쉬워진다.
        foreach (var prisoner in afterField.Prisoners)
        {
            afterField = FactionLifecycle.AdjustLoyalty(afterField, prisoner.General, -1);
        }

        turns = reports;
        sieges = siegeReports;
        captures = captureReports;
        plunders = plunderReports;
        casualties = casualtyReports;
        return _world.AdvanceDays(afterField, WeekDays);
    }

    // 성 보급: 아군 성 반경(_cityResupplyRadius) 안의 아군 야전 부대(군량 추적) 군량을 성 비축에서
    // 최대치까지 채운다. 성 비축 한도 안에서만. 결정론: 성 id 오름차순, 부대 id 오름차순.
    private (GameState, List<CombatUnit>) ResupplyFromCities(GameState work, List<CombatUnit> armies)
    {
        var byId = armies.ToDictionary(u => u.Id);
        var stock = work.Cities.ToDictionary(c => c.Id, c => c.Provisions);
        foreach (var city in work.Cities.OrderBy(c => c.Id.Value))
        {
            var have = stock[city.Id];
            if (have <= 0)
            {
                continue;
            }

            foreach (var unit in armies
                .Where(u => u.Field.Owner == city.Owner && u.TracksProvisions
                    && u.Field.Position.Distance(city.Position) <= _cityResupplyRadius)
                .OrderBy(u => u.Id.Value))
            {
                var cur = byId[unit.Id];
                var deficit = cur.MaxProvisions() - cur.Provisions;
                var give = System.Math.Min(deficit, have);
                if (give <= 0)
                {
                    continue;
                }

                byId[unit.Id] = cur with { Provisions = cur.Provisions + give };
                have -= give;
            }

            stock[city.Id] = have;
        }

        var cities = work.Cities.Select(c => c with { Provisions = stock[c.Id] }).ToList();
        return (work with { Cities = cities }, armies.Select(u => byId[u.Id]).ToList());
    }

    // 입성 부대 → 그 도시 대기 병력 편입(병종·훈련도 보존, 가중 평균) + 실린 장수 그 도시 주둔 복귀
    // + 예치: 노획 금·잔여 휴대 군량을 성 비축에 합산(design-administration "복귀 예치").
    private static GameState ApplyEntered(GameState work, IReadOnlyList<CombatUnit> entered,
        IReadOnlyDictionary<Spatial.HexCoord, CityId> cityAt)
    {
        if (entered.Count == 0)
        {
            return work;
        }

        var garrisons = work.Garrisons.ToList();
        var postings = work.Assignments.ToList();
        var cities = work.Cities.ToList();
        foreach (var unit in entered)
        {
            if (unit.Field.Target is not { } pos || !cityAt.TryGetValue(pos, out var cityId))
            {
                continue;
            }

            var gold = unit.LootGold;
            var provisions = unit.TracksProvisions ? unit.Provisions : 0;
            if (gold > 0 || provisions > 0)
            {
                var cIdx = cities.FindIndex(c => c.Id == cityId);
                if (cIdx >= 0)
                {
                    cities[cIdx] = cities[cIdx] with
                    {
                        Gold = cities[cIdx].Gold + gold,
                        Provisions = cities[cIdx].Provisions + provisions,
                    };
                }
            }

            var incoming = unit.IsSupply && unit.Cargo.Count > 0
                ? unit.Cargo.Select(c => (c.TroopCode, c.Troops, Training: c.TrainingLevel))
                : unit.TroopCode.Length > 0
                    ? [(unit.TroopCode, unit.Pool.Active, Training: unit.Training)]
                    : [];
            foreach (var (code, troops, training) in incoming)
            {
                if (troops <= 0)
                {
                    continue;
                }

                // 훈련도 50 미만이면 신병 풀로(방어적 — 현 규칙상 야전 부대는 50 이상).
                var trainee = training < 50;
                var idx = garrisons.FindIndex(g => g.City == cityId && g.TroopCode == code && g.Trainee == trainee);
                if (idx >= 0)
                {
                    garrisons[idx] = garrisons[idx].Merge(troops, training);
                }
                else
                {
                    garrisons.Add(new GarrisonForce(cityId, code, troops, training, trainee));
                }
            }

            foreach (var generalId in new[] { unit.VanguardId, unit.AdjutantId }.OfType<GeneralId>())
            {
                var pIdx = postings.FindIndex(p => p.General == generalId);
                if (pIdx >= 0)
                {
                    postings[pIdx] = postings[pIdx] with { Location = cityId };
                }
            }
        }

        return work with { GarrisonForces = garrisons, Postings = postings, Cities = cities };
    }
}
