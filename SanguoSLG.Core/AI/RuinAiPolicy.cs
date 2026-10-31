namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed record AiRuinCandidate(CityId Origin, string RuinId, HexCoord Exit,
    int RouteTiles, MovementDomain Domain);

public static class RuinAiPolicy
{
    public static bool ShouldAttack(RuinDefinition definition, RuinState status, FactionId faction,
        int day, int availableTroops)
        => !status.Registrations.Contains(faction)
           && !status.IsProtected(day)
           && status.Defenders > 0
           && availableTroops >= status.Defenders * 2L;

    public static bool CanStart(bool defending, bool supporting, bool ongoingOffense,
        bool newOffense)
        => !defending && !supporting && !ongoingOffense && !newOffense;

    public static IReadOnlyList<AiRuinCandidate> Candidates(GameState state,
        FactionId faction, HexMap map, BalanceConfig balance, bool eligibleOnly = false)
    {
        var blocked = state.Buildings.Where(building => !building.IsExpired(state.Day))
            .Select(building => building.Position)
            .Concat(state.Ruins.Select(ruin => ruin.Position));
        var passability = new PassabilityMap(map, [], state.Cities, blocked);
        var candidates = new List<(AiRuinCandidate Candidate, int Defenders)>();
        foreach (var city in state.Cities.Where(city => city.Owner == faction
            && AiSortiePolicy.AllowsCombatSortie(city, balance)).OrderBy(city => city.Id.Value))
        {
            var availableTroops = eligibleOnly ? AiWarForcePolicy.Available(state, city) : 0;
            foreach (var ruin in state.Ruins)
            {
                var status = state.RuinStatus.FirstOrDefault(entry => entry.RuinId == ruin.Id);
                if (status is null || status.Registrations.Contains(faction)
                    || status.IsProtected(state.Day) || status.Defenders <= 0
                    || eligibleOnly && !ShouldAttack(ruin, status, faction, state.Day,
                        availableTroops)) continue;
                var domain = ruin.Naval ? MovementDomain.DeepWater : MovementDomain.Land;
                if (ruin.Naval && (!city.IsPort || !state.PortShips.Any(ship =>
                    ship.City == city.Id && ship.Count > 0))) continue;
                if (!ruin.Naval && city.IsPort) continue;
                var exits = DeploymentEgressRules.ExteriorTiles(city)
                    .Where(exit => passability.CanEnter(domain, exit));
                var approaches = ruin.Position.Neighbors()
                    .Where(tile => passability.CanEnter(domain, tile)).ToArray();
                if (approaches.Length == 0) continue;
                var pathfinder = new HexPathfinder(tile => passability.CanEnter(domain, tile));
                var route = exits.SelectMany(exit => approaches.Select(approach =>
                        (Exit: exit, Path: pathfinder.FindPath(exit, approach))))
                    .Where(route => route.Path.Count > 0)
                    .OrderBy(route => route.Path.Count)
                    .ThenBy(route => route.Exit.Q).ThenBy(route => route.Exit.R)
                    .FirstOrDefault();
                if (route.Path is null || route.Path.Count == 0) continue;
                candidates.Add((new AiRuinCandidate(city.Id, ruin.Id, route.Exit,
                    route.Path.Count + 1, domain), status.Defenders));
            }
        }
        return candidates.OrderBy(candidate => candidate.Candidate.Origin.Value)
            .ThenBy(candidate => candidate.Defenders)
            .ThenBy(candidate => candidate.Candidate.RouteTiles)
            .ThenBy(candidate => candidate.Candidate.RuinId, StringComparer.Ordinal)
            .Select(candidate => candidate.Candidate).ToList();
    }
}
