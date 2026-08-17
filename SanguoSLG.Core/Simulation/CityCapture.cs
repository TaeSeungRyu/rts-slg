namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>한 도시 함락(점거) 보고.</summary>
/// <param name="City">함락된 도시.</param>
/// <param name="NewOwner">점거 세력.</param>
/// <param name="OldOwner">옛 소유 세력.</param>
/// <param name="Captured">포로가 된 주둔 장수.</param>
/// <param name="Fled">원 세력 다른 도시로 후퇴한 주둔 장수.</param>
/// <param name="FactionEliminated">이 함락으로 옛 세력이 소멸했는가.</param>
public sealed record CaptureReport(
    CityId City, FactionId NewOwner, FactionId OldOwner,
    IReadOnlyList<GeneralId> Captured, IReadOnlyList<GeneralId> Fled, bool FactionEliminated);

/// <summary>
/// 캠페인 함락 처리(design-general-lifecycle §4·design-combat "함락 처리"). 성벽 0 + 수비 0 도시에
/// 근접(거리 1) 적 공격 부대가 있으면 자동 입성·점거한다. 소유 전환 + 자원 전부 승계(인구 −10%·
/// 치안 30 리셋), 진행 중 명령 드롭, 주둔 장수 개별 판정(50% 포로 / 50% 원 세력 최근접 도시 후퇴,
/// 원 도시 0이면 세력 소멸). 결정론: 도시·부대 id 순, 50% 판정은 시드 난수만.
/// </summary>
public sealed class CityCapture
{
    private readonly int _populationLossPercent;
    private readonly int _security;

    public CityCapture(int populationLossPercent = 10, int security = 30)
    {
        _populationLossPercent = populationLossPercent;
        _security = security;
    }

    /// <summary>점거 가능한 도시를 전부 처리한 새 상태를 반환한다(id 순).</summary>
    public GameState ResolveAll(GameState state, IRandomSource random, out IReadOnlyList<CaptureReport> reports)
    {
        var list = new List<CaptureReport>();
        foreach (var city in state.Cities.OrderBy(c => c.Id.Value).ToList())
        {
            var current = state.Cities.FirstOrDefault(c => c.Id == city.Id);
            if (current is null || current.Wall > 0)
            {
                continue;
            }

            if (state.Garrisons.Where(g => g.City == current.Id).Sum(g => g.Troops) > 0)
            {
                continue; // 수비가 남아 있으면 아직 함락 아님
            }

            var besiegers = state.Armies
                .Where(u => u.Pool.Active > 0 && u.Field.Owner != current.Owner
                    && u.Field.Mode == UnitMode.Attack && !u.IsSupply
                    && u.Field.Position.Distance(current.Position) <= 1)
                .OrderBy(u => u.Id.Value)
                .ToList();
            if (besiegers.Count == 0)
            {
                continue; // 근접 공격군 없으면 빈 성으로 남는다
            }

            var captor = besiegers[0].Field.Owner; // 최저 id 부대의 세력이 점거
            var entrants = besiegers.Where(u => u.Field.Owner == captor).ToList();
            state = Capture(state, current, captor, entrants, random, list);
        }

        reports = list;
        return state;
    }

    private GameState Capture(GameState state, City city, FactionId captor,
        IReadOnlyList<CombatUnit> entrants, IRandomSource random, List<CaptureReport> reports)
    {
        var oldOwner = city.Owner;

        // 1) 소유 전환 + 자원 전부 승계(인구 −10%·치안 30 리셋, 성벽은 0 유지).
        var newPopulation = city.Population - city.Population * _populationLossPercent / 100;
        var cities = state.Cities
            .Select(c => c.Id == city.Id
                ? c with { Owner = captor, Population = newPopulation, Security = _security }
                : c)
            .ToList();

        // 2) 입성 부대 → 야전에서 빠지고 수비대(병종별)로 편입, 실린 장수는 이 도시 주둔.
        var entrantIds = entrants.Select(u => u.Id).ToHashSet();
        var armies = state.Armies.Where(u => !entrantIds.Contains(u.Id)).ToList();
        var garrisons = state.Garrisons.ToList();
        var postings = state.Assignments.ToList();
        foreach (var unit in entrants.OrderBy(u => u.Id.Value))
        {
            AddGarrison(garrisons, city.Id, unit.TroopCode, unit.Pool.Active, unit.Training);
            foreach (var gid in new[] { unit.VanguardId, unit.AdjutantId }.OfType<GeneralId>())
            {
                var idx = postings.FindIndex(p => p.General == gid);
                if (idx >= 0)
                {
                    postings[idx] = postings[idx] with { Location = city.Id };
                }
            }
        }

        // 3) 진행 중 명령 드롭(그 도시) — 수행 장수는 자동으로 잠금 해제된다.
        var pending = state.Commands.Where(c => c.City != city.Id).ToList();

        var next = state with
        {
            Cities = cities,
            FieldArmies = armies,
            GarrisonForces = garrisons,
            Postings = postings,
            PendingCommands = pending,
        };

        // 4) 옛 세력 주둔 장수(태수 포함) 개별 판정. 옛 세력이 도시를 모두 잃으면 세력 소멸.
        var stationed = next.Assignments
            .Where(p => p.Faction == oldOwner && p.Location == city.Id)
            .Select(p => p.General)
            .OrderBy(g => g.Value)
            .ToList();

        var captured = new List<GeneralId>();
        var fled = new List<GeneralId>();

        if (next.CityCount(oldOwner) == 0)
        {
            next = FactionLifecycle.EliminateFaction(next, oldOwner); // 전원 재야
            reports.Add(new CaptureReport(city.Id, captor, oldOwner, captured, fled, FactionEliminated: true));
            return next;
        }

        foreach (var gid in stationed)
        {
            if (random.Next(0, 2) == 0)
            {
                next = FactionLifecycle.MakePrisoner(next, gid, holder: captor, origin: oldOwner);
                captured.Add(gid);
            }
            else
            {
                var refuge = NearestOwnedCity(next, oldOwner, city.Position);
                var idx = next.Assignments.ToList().FindIndex(p => p.General == gid);
                if (refuge is { } dest && idx >= 0)
                {
                    var postings2 = next.Assignments.Select(p => p.General == gid ? p with { Location = dest } : p).ToList();
                    next = next with { Postings = postings2 };
                }

                fled.Add(gid);
            }
        }

        reports.Add(new CaptureReport(city.Id, captor, oldOwner, captured, fled, FactionEliminated: false));
        return next;
    }

    private static void AddGarrison(List<GarrisonForce> garrisons, CityId city, string troopCode, int troops, int training)
    {
        if (troopCode.Length == 0 || troops <= 0)
        {
            return;
        }

        var idx = garrisons.FindIndex(g => g.City == city && g.TroopCode == troopCode);
        if (idx >= 0)
        {
            garrisons[idx] = garrisons[idx].Merge(troops, training);
        }
        else
        {
            garrisons.Add(new GarrisonForce(city, troopCode, troops, training));
        }
    }

    // 원 세력의 가장 가까운 보유 도시(헥사 거리 최소, 동률 id순 — 결정론). 없으면 null.
    private static CityId? NearestOwnedCity(GameState state, FactionId faction, HexCoord from)
    {
        var owned = state.Cities.Where(c => c.Owner == faction)
            .OrderBy(c => c.Position.Distance(from)).ThenBy(c => c.Id.Value)
            .FirstOrDefault();
        return owned?.Id;
    }
}
