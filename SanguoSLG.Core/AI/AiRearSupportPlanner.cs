namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

/// <summary>후방 잉여 병력을 기존 수송 명령으로 전선에 보내되 방어 예비와 중복 수송을 보존한다.</summary>
public sealed class AiRearSupportPlanner(DeployService deployer, HexMap? map = null)
{
    public GameState Plan(GameState state, FactionId faction)
    {
        state = RedirectLostDestinations(state, faction);
        var hostile = state.Cities.Where(c => state.AreHostile(faction, c.Owner)
            && state.IsScouted(faction, c.Id)).ToList();
        if (hostile.Count == 0) return state;
        var fronts = state.Cities.Where(c => c.Owner == faction && !c.IsPort
            && hostile.Any(enemy => c.Position.Distance(enemy.Position) <= 6))
            .OrderBy(c => c.Id.Value).ToList();
        foreach (var front in fronts)
        {
            if (state.Armies.Any(u => u.IsTransport && u.Field.Owner == faction
                && u.Field.ReturnCity == front.Id)) continue;
            var enemy = hostile.OrderBy(c => c.Position.Distance(front.Position)).First();
            var frontTroops = state.Garrisons.Where(g => g.City == front.Id && !g.Trainee)
                .Sum(g => g.Troops);
            var need = Math.Max(0, (int)Math.Ceiling(state.Garrisons
                .Where(g => g.City == enemy.Id).Sum(g => g.Troops) * 1.2) - frontTroops);
            if (need < 1000) continue;
            foreach (var rear in state.Cities.Where(c => c.Owner == faction && !c.IsPort
                && c.Id != front.Id && hostile.All(e => c.Position.Distance(e.Position) > 6))
                .OrderBy(c => c.Position.Distance(front.Position)).ThenBy(c => c.Id.Value))
            {
                var stationed = state.Garrisons.Where(g => g.City == rear.Id && !g.Trainee)
                    .Sum(g => g.Troops);
                var surplus = stationed - Math.Max(10_000, (int)Math.Ceiling(stationed * .30));
                if (surplus < 1000) continue;
                var officer = state.GeneralsAt(rear.Id).Where(g => !state.IsGeneralBusy(g))
                    .OrderBy(g => g.Value).FirstOrDefault();
                if (officer == default) continue;
                var route = Route(state, rear, front);
                if (route is null) continue;
                var line = state.Garrisons.Where(g => g.City == rear.Id && !g.Trainee
                    && g.Troops >= 1000 && deployer.Troops.TryGetValue(g.TroopCode, out var troop)
                    && troop.Class is not (TroopClass.Naval or TroopClass.Supply))
                    .OrderByDescending(g => g.Troops).FirstOrDefault();
                if (line is null) continue;
                var count = Math.Min(Math.Min(need, surplus), Math.Min(line.Troops, 50_000));
                var food = Math.Max(10, (count + 999) / 1000 * ((route.Value.Distance + 1) / 2 + 3));
                var rearFoodReserve = Math.Max(30, (stationed + 999) / 1000 * 30);
                if (rear.Provisions < food + rearFoodReserve) continue;
                var frontFoodNeed = Math.Max(0,
                    Math.Max(300, (frontTroops + 999) / 1000 * 30) - front.Provisions);
                var cargoFood = Math.Min(frontFoodNeed,
                    Math.Max(0, rear.Provisions - food - rearFoodReserve));
                var cargoGold = Math.Min(Math.Max(0, 1000 - front.Gold),
                    Math.Max(0, rear.Gold - 1000));
                var result = deployer.DeployTransport(state, new TransportDeployRequest(rear.Id,
                    [new TransportLine(line.TroopCode, count)], front.Id, officer,
                    Gold: cargoGold, Provisions: food + cargoFood,
                    EgressExit: route.Value.Exit));
                if (!result.Ok) continue;
                state = result.State;
                break;
            }
        }
        return state;
    }

    private static GameState RedirectLostDestinations(GameState state, FactionId faction)
    {
        return state with { FieldArmies = state.Armies.Select(unit =>
        {
            if (!unit.IsTransport || unit.Field.Owner != faction
                || unit.Field.ReturnCity is not { } destinationId
                || state.Cities.Any(city => city.Id == destinationId && city.Owner == faction))
                return unit;
            var safe = state.Cities.Where(city => city.Owner == faction)
                .OrderBy(city => city.Id == unit.OriginCity ? 0 : 1)
                .ThenBy(city => city.Position.Distance(unit.Field.Position))
                .ThenBy(city => city.Id.Value).FirstOrDefault();
            return unit with { Field = unit.Field with
            {
                Mode = safe is null ? UnitMode.Standby : UnitMode.March,
                Target = safe?.Position,
                ReturnCity = safe?.Id,
                Waypoints = null,
                ContinuousTarget = null,
                ContinuousWaypoints = null,
            } };
        }).ToList() };
    }

    private (HexCoord Exit, int Distance)? Route(GameState state, City origin, City target)
    {
        if (map is null)
        {
            var exit = DeploymentEgressRules.RepresentativeExit(origin,
                DeploymentEgressRules.Recommend(origin, target.Position), target.Position);
            return exit is { } point ? (point, origin.Position.Distance(target.Position)) : null;
        }
        var passable = new PassabilityMap(map, [], state.Cities,
            state.Buildings.Where(b => !b.IsExpired(state.Day)).Select(b => b.Position)
                .Concat(state.Ruins.Select(r => r.Position)));
        var exits = DeploymentEgressRules.ExteriorTiles(origin)
            .Where(p => passable.CanEnter(MovementDomain.Land, p)).ToList();
        var goals = DeploymentEgressRules.ExteriorTiles(target)
            .Where(p => passable.CanEnter(MovementDomain.Land, p)).ToHashSet();
        var queue = new Queue<(HexCoord Position, HexCoord Exit, int Distance)>();
        var seen = new HashSet<HexCoord>();
        foreach (var exit in exits)
            if (seen.Add(exit)) queue.Enqueue((exit, exit, 1));
        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            if (goals.Contains(point.Position)) return (point.Exit, point.Distance + 1);
            if (point.Distance >= 30) continue;
            foreach (var next in point.Position.Neighbors())
                if (seen.Add(next) && passable.CanEnter(MovementDomain.Land, next))
                    queue.Enqueue((next, point.Exit, point.Distance + 1));
        }
        return null;
    }
}
