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

    public CampaignEngine(AdvanceOrchestrator field, WorldEngine world)
    {
        _field = field;
        _world = world;
    }

    /// <summary>7일을 진행한 새 상태를 반환한다. 야전 진행 보고 목록은 <paramref name="turns"/>로.</summary>
    public GameState AdvanceWeek(GameState state, out IReadOnlyList<AdvanceTurn> turns)
    {
        var reports = new List<AdvanceTurn>();
        var armies = state.Armies.Where(u => u.Pool.Active > 0).ToList();
        var garrisons = state.Garrisons.ToList();

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
            foreach (var entered in turn.EnteredCastle)
            {
                if (entered.Field.Target is { } pos && cityAt.TryGetValue(pos, out var cityId)
                    && entered.TroopCode.Length > 0)
                {
                    var idx = garrisons.FindIndex(g => g.City == cityId && g.TroopCode == entered.TroopCode);
                    if (idx >= 0)
                    {
                        garrisons[idx] = garrisons[idx].Merge(entered.Pool.Active, entered.Training);
                    }
                    else
                    {
                        garrisons.Add(new GarrisonForce(cityId, entered.TroopCode, entered.Pool.Active, entered.Training));
                    }
                }
            }
        }

        var afterField = state with
        {
            FieldArmies = armies,
            GarrisonForces = garrisons
                .Where(g => g.Troops > 0)
                .OrderBy(g => g.City.Value).ThenBy(g => g.TroopCode, System.StringComparer.Ordinal)
                .ToList(),
        };

        turns = reports;
        return _world.AdvanceDays(afterField, WeekDays);
    }
}
