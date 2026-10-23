namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed record AiWarCandidate(CityId Origin, CityId Target, int RouteTiles,
    int TravelDays, MovementDomain Domain, HexCoord Exit, bool EmptyGarrison,
    int DefenderTroops = 0);

public static class AiWarCandidatePolicy
{
    public static bool CanLaunch(City city, BalanceConfig balance) => city.Security >= 70
        && city.Wall * 2 >= CastleWall.Max(city.Castle, balance, city.WallLevel);

    public static IReadOnlyList<AiWarCandidate> Evaluate(GameState state, FactionId faction,
        HexMap map, BalanceConfig balance, int slowestSpeed)
    {
        if (slowestSpeed <= 0) return [];
        var blocked = state.Buildings.Where(building => !building.IsExpired(state.Day))
            .Select(building => building.Position)
            .Concat(state.Ruins.Select(ruin => ruin.Position));
        var passability = new PassabilityMap(map, [], state.Cities, blocked);
        var candidates = new List<AiWarCandidate>();
        foreach (var origin in state.Cities.Where(city => city.Owner == faction
            && CanLaunch(city, balance)).OrderBy(city => city.Id.Value))
        {
            foreach (var target in state.Cities.Where(city => state.AreHostile(faction, city.Owner)
                && state.IsScouted(faction, city.Id)).OrderBy(city => city.Id.Value))
            {
                var land = FindRoute(origin, target, passability, MovementDomain.Land,
                    slowestSpeed);
                var sea = origin.IsPort && target.IsPort
                    && state.PortShips.Any(stock => stock.City == origin.Id && stock.Count > 0)
                    ? FindRoute(origin, target, passability, MovementDomain.DeepWater,
                        slowestSpeed)
                    : null;
                var route = new[] { land, sea }.OfType<AiWarCandidate>()
                    .OrderBy(candidate => candidate.TravelDays)
                    .ThenBy(candidate => candidate.RouteTiles)
                    .ThenBy(candidate => candidate.Domain).FirstOrDefault();
                if (route is null) continue;
                var defenders = state.Garrisons.Where(line => line.City == target.Id)
                    .Sum(line => Math.Max(0, line.Troops));
                candidates.Add(route with { EmptyGarrison = defenders == 0,
                    DefenderTroops = defenders });
            }
        }
        return candidates.OrderBy(candidate => candidate.RouteTiles)
            .ThenBy(candidate => candidate.Origin.Value)
            .ThenBy(candidate => candidate.Target.Value).ToList();
    }

    public static IReadOnlyList<AiWarCandidate> ChooseTargets(
        IEnumerable<AiWarCandidate> candidates, int day, FactionId faction, CityId origin)
    {
        var choices = candidates.Where(candidate => candidate.Origin == origin)
            .OrderByDescending(candidate => candidate.EmptyGarrison)
            .ThenBy(candidate => candidate.DefenderTroops)
            .ThenBy(candidate => candidate.RouteTiles)
            .ThenBy(candidate => candidate.Target.Value).ToList();
        var seed = unchecked(day * 397 ^ faction.Value * 31 ^ origin.Value);
        var count = new SeededRandomSource(seed).Next(1, 3);
        return choices.Take(count).OrderBy(candidate => candidate.EmptyGarrison ? 0 : 1)
            .ThenBy(candidate => candidate.DefenderTroops)
            .ThenBy(candidate => candidate.RouteTiles)
            .ThenBy(candidate => candidate.Target.Value).ToList();
    }

    private static AiWarCandidate? FindRoute(City origin, City target,
        PassabilityMap passability, MovementDomain domain, int speed)
    {
        var exits = DeploymentEgressRules.ExteriorTiles(origin)
            .Where(tile => passability.CanEnter(domain, tile))
            .OrderBy(tile => tile.Q).ThenBy(tile => tile.R).ToList();
        var approaches = DeploymentEgressRules.ExteriorTiles(target)
            .Where(tile => passability.CanEnter(domain, tile)).ToHashSet();
        if (exits.Count == 0 || approaches.Count == 0) return null;
        var queue = new Queue<(HexCoord Tile, HexCoord Exit, int Steps)>();
        var seen = new HashSet<HexCoord>();
        foreach (var exit in exits)
            if (seen.Add(exit)) queue.Enqueue((exit, exit, 1));
        while (queue.Count > 0)
        {
            var (tile, exit, steps) = queue.Dequeue();
            if (approaches.Contains(tile))
            {
                var routeTiles = steps + 1;
                var days = (routeTiles + speed - 1) / speed;
                return days <= 30
                    ? new AiWarCandidate(origin.Id, target.Id, routeTiles, days,
                        domain, exit, EmptyGarrison: false)
                    : null;
            }
            if (steps >= 30 * speed) continue;
            foreach (var next in tile.Neighbors().OrderBy(hex => hex.Q).ThenBy(hex => hex.R))
                if (seen.Add(next) && passability.CanEnter(domain, next))
                    queue.Enqueue((next, exit, steps + 1));
        }
        return null;
    }
}
