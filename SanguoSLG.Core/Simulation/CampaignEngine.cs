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

    public CampaignEngine(AdvanceOrchestrator field, WorldEngine world, CampaignSiege? siege = null)
    {
        _field = field;
        _world = world;
        _siege = siege;
    }

    /// <summary>7일을 진행한 새 상태를 반환한다. 야전 진행 보고 목록은 <paramref name="turns"/>로.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns)
        => AdvanceWeek(state, out turns, out _);

    /// <summary>7일 진행 + 공성 교환 보고(<paramref name="sieges"/>)까지 돌려주는 오버로드.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns,
        out IReadOnlyList<SiegeExchange> sieges)
    {
        var reports = new List<AdvanceTurn>();
        var siegeReports = new List<SiegeExchange>();
        var armies = state.Armies.Where(u => u.Pool.Active > 0).ToList();
        var garrisons = state.Garrisons.ToList();
        var postings = state.Assignments.ToList();
        var cities = state.Cities.ToList();

        var castles = state.Cities
            .OrderBy(c => c.Id.Value)
            .Select(c => new SiegeSite(c.Position, c.Owner))
            .ToList();
        var cityAt = state.Cities.ToDictionary(c => c.Position, c => c.Id);

        var remaining = WeekDays;
        while (remaining > 0 && armies.Count > 0)
        {
            var turn = _field.Run(armies, maxDays: remaining, castles);
            reports.Add(turn);
            remaining -= System.Math.Max(1, turn.Movement.Days);
            armies = turn.Units.ToList();

            // 입성 부대 → 그 도시의 대기 병력으로 편입(병종·훈련도 보존, 훈련도는 가중 평균).
            // 실려 있던 장수(선봉·부관)는 그 도시 주둔으로 복귀한다.
            foreach (var entered in turn.EnteredCastle)
            {
                if (entered.Field.Target is { } pos && cityAt.TryGetValue(pos, out var cityId))
                {
                    // 보급부대는 병종별 구성대로, 일반 부대는 단일 병종으로 편입.
                    var incoming = entered.IsSupply && entered.Cargo.Count > 0
                        ? entered.Cargo.Select(c => (c.TroopCode, c.Troops, Training: c.TrainingLevel))
                        : entered.TroopCode.Length > 0
                            ? [(entered.TroopCode, entered.Pool.Active, Training: entered.Training)]
                            : [];
                    foreach (var (code, troops, training) in incoming)
                    {
                        if (troops <= 0)
                        {
                            continue;
                        }

                        var idx = garrisons.FindIndex(g => g.City == cityId && g.TroopCode == code);
                        if (idx >= 0)
                        {
                            garrisons[idx] = garrisons[idx].Merge(troops, training);
                        }
                        else
                        {
                            garrisons.Add(new GarrisonForce(cityId, code, troops, training));
                        }
                    }

                    foreach (var generalId in new[] { entered.VanguardId, entered.AdjutantId }.OfType<GeneralId>())
                    {
                        var pIdx = postings.FindIndex(p => p.General == generalId);
                        if (pIdx >= 0)
                        {
                            postings[pIdx] = postings[pIdx] with { Location = cityId };
                        }
                    }
                }
            }

            // 공성 교환(design-combat "성 전투") — 접적으로 멈춘 공격 부대가 성벽·수비를 깎는다.
            // 소유 전환·함락은 다음 단계. 반격으로 병력 0이 된 부대는 다음 진행에서 빠진다.
            if (_siege is not null)
            {
                var result = _siege.Resolve(armies, cities, garrisons);
                armies = result.Armies.Where(u => u.Pool.Active > 0).ToList();
                cities = result.Cities.ToList();
                garrisons = result.Garrisons.ToList();
                siegeReports.AddRange(result.Exchanges);
            }
        }

        var afterField = state with
        {
            FieldArmies = armies,
            GarrisonForces = garrisons
                .Where(g => g.Troops > 0)
                .OrderBy(g => g.City.Value).ThenBy(g => g.TroopCode, System.StringComparer.Ordinal)
                .ToList(),
            Postings = postings,
            Cities = cities,
        };

        // 포로 충성 하락(일주일 −1 — design-general-lifecycle §2): 억류될수록 등용이 쉬워진다.
        foreach (var prisoner in afterField.Prisoners)
        {
            afterField = FactionLifecycle.AdjustLoyalty(afterField, prisoner.General, -1);
        }

        turns = reports;
        sieges = siegeReports;
        return _world.AdvanceDays(afterField, WeekDays);
    }
}
