namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.Climate;
using SanguoSLG.Core.Spatial;

public sealed record AiSupportRoute(CityId Origin, CityId Defended, HexCoord Exit,
    int TravelDays, string TroopCode);

public static class AiSupportRoutePolicy
{
    public static IReadOnlyList<CombatUnit> Attackers(GameState state, City defended)
    {
        var footprint = CastleFootprint.TilesFor(defended).ToList();
        return state.Armies.Where(unit => unit.Pool.Active > 0 && !unit.IsWaitingDeployment
                && unit.CanInitiateCombat && state.AreHostile(defended.Owner, unit.Field.Owner)
                && unit.Field.Mode == UnitMode.Attack
                && unit.Field.Target is { } target && footprint.Contains(target)
                && footprint.Min(tile => tile.Distance(unit.Field.Position)) <= unit.Field.RangeCastle)
            .OrderBy(unit => unit.Id.Value).ToList();
    }

    public static AiSupportRoute? Closest(GameState state, City defended, HexMap map,
        IReadOnlyDictionary<string, TroopTemplate> troops, BalanceConfig balance,
        int minimumTraining = 0)
    {
        var blocked = state.Buildings.Where(building => !building.IsExpired(state.Day))
            .Select(building => building.Position).Concat(state.Ruins.Select(ruin => ruin.Position));
        var passability = new PassabilityMap(map, [], state.Cities, blocked);
        var approaches = DeploymentEgressRules.ExteriorTiles(defended)
            .Where(tile => passability.CanEnter(MovementDomain.Land, tile)).ToHashSet();
        if (approaches.Count == 0) return null;
        var climate = state.Climate is null ? null : new ClimateController(map);
        var candidates = new List<AiSupportRoute>();
        foreach (var origin in state.Cities.Where(city => city.Owner == defended.Owner
                     && !city.IsPort && city.Id != defended.Id
                     && AiSortiePolicy.AllowsCombatSortie(city, balance))
                 .OrderBy(city => city.Id.Value))
        {
            var available = state.Garrisons.Where(line => line.City == origin.Id && !line.Trainee)
                .Sum(line => Math.Max(0, line.Troops));
            var allowance = available * 40 / 100;
            if (allowance < 6000) continue;
            foreach (var line in state.Garrisons.Where(line => line.City == origin.Id
                         && !line.Trainee && line.Troops >= 6000
                         && line.TrainingLevel >= minimumTraining
                         && troops.TryGetValue(line.TroopCode, out var troop)
                         && troop.Class is TroopClass.Cavalry or TroopClass.Infantry or TroopClass.Archer)
                     .OrderBy(line => line.TroopCode, StringComparer.Ordinal))
            {
                var troop = troops[line.TroopCode];
                if (Math.Min(line.Troops, allowance) < 6000) continue;
                var route = FindRoute(origin, approaches, passability, map, state.Climate,
                    climate, troop.MovementPerDay);
                if (route is { } found && found.TravelDays <= 14)
                    candidates.Add(new AiSupportRoute(origin.Id, defended.Id, found.Exit,
                        found.TravelDays, line.TroopCode));
            }
        }
        return candidates.OrderBy(candidate => candidate.TravelDays)
            .ThenBy(candidate => candidate.Origin.Value)
            .ThenBy(candidate => ClassPriority(troops[candidate.TroopCode].Class))
            .ThenBy(candidate => candidate.TroopCode, StringComparer.Ordinal).FirstOrDefault();
    }

    public static int ClassPriority(TroopClass troopClass) => troopClass switch
    {
        TroopClass.Cavalry => 0,
        TroopClass.Infantry => 1,
        TroopClass.Archer => 2,
        _ => 3,
    };

    private static (HexCoord Exit, int TravelDays)? FindRoute(City origin,
        IReadOnlySet<HexCoord> approaches, PassabilityMap passability, HexMap map,
        ClimateState? climateState, ClimateController? climate, int speed)
    {
        if (speed <= 0) return null;
        var queue = new PriorityQueue<(HexCoord Tile, HexCoord Exit, long Cost), (long, int, int)>();
        var best = new Dictionary<HexCoord, long>();
        foreach (var exit in DeploymentEgressRules.ExteriorTiles(origin)
                     .Where(tile => passability.CanEnter(MovementDomain.Land, tile)))
        {
            if (!best.TryAdd(exit, 1000)) continue;
            queue.Enqueue((exit, exit, 1000), (1000, exit.Q, exit.R));
        }
        while (queue.TryDequeue(out var item, out _))
        {
            if (item.Cost != best[item.Tile]) continue;
            if (approaches.Contains(item.Tile))
            {
                var days = (int)((item.Cost + 1000 + speed * 1000L - 1) / (speed * 1000L));
                return (item.Exit, days);
            }
            if (item.Cost > 14L * speed * 1000) continue;
            foreach (var next in item.Tile.Neighbors().OrderBy(tile => tile.Q).ThenBy(tile => tile.R))
            {
                if (!passability.CanEnter(MovementDomain.Land, next)) continue;
                var percent = map.TerrainAt(next) is TerrainType.Mountain or TerrainType.Swamp or TerrainType.River
                    ? 50 : 100;
                if (climateState is not null && climate is not null
                    && climate.At(climateState, next) != ClimateKind.Clear)
                    percent = percent * 70 / 100;
                var cost = item.Cost + (100_000 + percent - 1) / percent;
                if (best.TryGetValue(next, out var previous) && previous <= cost) continue;
                best[next] = cost;
                queue.Enqueue((next, item.Exit, cost), (cost, next.Q, next.R));
            }
        }
        return null;
    }
}
