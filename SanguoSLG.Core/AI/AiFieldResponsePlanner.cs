namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

/// <summary>Phase 21 야전 대응: 위협받는 성을 다른 성의 기병으로 요격하고 고립 보급대를 우선한다.</summary>
public sealed class AiFieldResponsePlanner
{
    private readonly DeployService _deployer;
    private readonly AiConfig _config;
    private readonly HexMap? _map;
    private readonly BalanceConfig? _economy;

    public AiFieldResponsePlanner(DeployService deployer, AiConfig config, HexMap? map = null,
        BalanceConfig? economy = null)
    {
        _deployer = deployer;
        _config = config;
        _map = map;
        _economy = economy;
    }

    public GameState Plan(GameState state, FactionId faction)
    {
        foreach (var threatened in state.Cities.Where(c => c.Owner == faction && !c.IsPort)
            .OrderBy(c => c.Id.Value))
        {
            var intruders = state.Armies.Where(u => u.Pool.Active > 0 && !u.IsWaitingDeployment
                    && state.AreHostile(faction, u.Field.Owner)
                    && threatened.Position.Distance(u.Field.Position) <= 4)
                .OrderBy(u => u.Field.Position.Distance(threatened.Position))
                .ThenBy(u => u.Id.Value).ToList();
            if (intruders.Count == 0) continue;

            var targets = intruders.Where(u => u.IsSupply && !Escorted(state, u)).Concat(intruders
                .Where(u => !u.IsSupply)).ToList();
            foreach (var target in targets)
            {
                if (state.Armies.Any(u => u.Field.Owner == faction
                    && u.Field.AssignedUnitTarget == target.Id)) continue;
                foreach (var city in state.Cities.Where(c => c.Owner == faction && !c.IsPort
                        && c.Id != threatened.Id
                        && c.Position.Distance(threatened.Position) <= 6)
                    .OrderBy(c => c.Position.Distance(target.Field.Position))
                    .ThenBy(c => c.Id.Value))
                {
                    var next = TryIntercept(state, city, target);
                    if (next is null) continue;
                    state = next;
                    break;
                }
            }
        }
        // 별도 기병 견제: 성 수비 전력이 아닌 안전한 후방 기병으로, 정찰망에 잡힌
        // 고립 야전 부대만 추격한다. 성·건축물은 표적으로 삼지 않는다.
        foreach (var target in state.Armies.Where(u => u.Pool.Active > 0 && !u.IsWaitingDeployment
                && state.AreHostile(faction, u.Field.Owner) && !Escorted(state, u)
                && state.Armies.Any(spotter => spotter.Field.Owner == faction
                    && !spotter.IsWaitingDeployment && spotter.Pool.Active > 0
                    && spotter.Field.Position.Distance(u.Field.Position)
                        <= spotter.Field.Detection))
            .OrderByDescending(u => u.IsSupply).ThenBy(u => u.Id.Value))
        {
            if (state.Armies.Any(u => u.Field.Owner == faction
                && u.Field.AssignedUnitTarget == target.Id)) continue;
            foreach (var origin in state.Cities.Where(c => c.Owner == faction && !c.IsPort
                    && c.Position.Distance(target.Field.Position) <= 8
                    && !state.Armies.Any(enemy => enemy.Pool.Active > 0
                        && state.AreHostile(faction, enemy.Field.Owner)
                        && c.Position.Distance(enemy.Field.Position) <= 4))
                .OrderBy(c => c.Position.Distance(target.Field.Position)).ThenBy(c => c.Id.Value))
            {
                var next = TryIntercept(state, origin, target);
                if (next is null) continue;
                state = next;
                break;
            }
        }
        return state;
    }

    private GameState? TryIntercept(GameState state, City origin, CombatUnit target)
    {
        if (!AiSortiePolicy.AllowsCombatSortie(origin, _economy)) return null;
        var cavalry = state.Garrisons.Where(g => g.City == origin.Id && !g.Trainee
                && g.TrainingLevel >= _deployer.Balance.DeployMinTraining
                && _deployer.Troops.TryGetValue(g.TroopCode, out var template)
                && template.Class == TroopClass.Cavalry)
            .OrderByDescending(g => g.Troops).ThenBy(g => g.TroopCode, StringComparer.Ordinal)
            .FirstOrDefault();
        if (cavalry is null) return null;
        var total = state.Garrisons.Where(g => g.City == origin.Id && !g.Trainee).Sum(g => g.Troops);
        var reserve = Math.Max(10_000, (int)Math.Ceiling(total * 0.30));
        var deployable = Math.Min(cavalry.Troops, Math.Max(0, total - reserve));
        deployable = Math.Min(deployable, CommandEfficiency.CommandTroopDeployLimit(
            state.ResearchOf(origin.Owner, FactionResearch.CommandTroopsCode), _deployer.Balance));
        if (deployable < 1000 || deployable < target.Pool.Active * (target.IsSupply ? 0.5 : 1.0))
            return null;
        var speed = Math.Max(1, _deployer.Troops[cavalry.TroopCode].MovementPerDay);
        var route = InterceptRoute(state, origin, target, speed * 3);
        if (_map is not null && route is null) return null;
        if (_map is null && origin.Position.Distance(target.Field.Position) > speed * 3) return null;
        var officers = state.GeneralsAt(origin.Id).Where(id => !state.IsGeneralBusy(id))
            .Select(id => state.Generals.First(g => g.Id == id))
            .OrderByDescending(g => g.AptitudeFor(TroopClass.Cavalry))
            .ThenByDescending(g => g.Might).ThenBy(g => g.Id.Value).ToList();
        if (officers.Count <= _config.KeepGeneralsHome) return null;

        var result = _deployer.Deploy(state, new DeployRequest(origin.Id, cavalry.TroopCode,
            deployable, officers[0].Id, Mode: UnitMode.Attack,
            Target: target.Field.Position, EgressExit: route));
        if (!result.Ok) return null;
        var id = result.State.Armies[^1].Id;
        return result.State with { FieldArmies = result.State.Armies.Select(u => u.Id == id
            ? u with { Field = u.Field with { AssignedUnitTarget = target.Id,
                ReturnCity = origin.Id } } : u).ToList() };
    }

    private HexCoord? InterceptRoute(GameState state, City origin, CombatUnit target,
        int maxSteps)
    {
        if (_map is null) return null;
        var blocked = state.Buildings.Where(b => !b.IsExpired(state.Day)).Select(b => b.Position)
            .Concat(state.Ruins.Select(r => r.Position));
        var passability = new PassabilityMap(_map, [], state.Cities, blocked);
        var danger = target.IsSupply ? state.Armies.Where(u => u.Id != target.Id
            && u.Pool.Active > 0 && state.AreHostile(origin.Owner, u.Field.Owner))
            .SelectMany(u => u.Field.Position.Neighbors().Append(u.Field.Position)).ToHashSet()
            : new HashSet<HexCoord>();
        var exits = DeploymentEgressRules.ExteriorTiles(origin)
            .Where(tile => passability.CanEnter(MovementDomain.Land, tile)
                && !danger.Contains(tile)).OrderBy(tile => tile.Q).ThenBy(tile => tile.R);
        var goals = target.Field.Position.Neighbors()
            .Where(tile => passability.CanEnter(MovementDomain.Land, tile)
                && !danger.Contains(tile)).ToHashSet();
        var queue = new Queue<(HexCoord Position, HexCoord Exit, int Steps)>();
        var seen = new HashSet<HexCoord>();
        foreach (var exit in exits)
            if (seen.Add(exit)) queue.Enqueue((exit, exit, 1));
        while (queue.Count > 0)
        {
            var (position, exit, steps) = queue.Dequeue();
            if (goals.Contains(position)) return exit;
            if (steps >= maxSteps) continue;
            foreach (var next in position.Neighbors())
                if (seen.Add(next) && !danger.Contains(next)
                    && passability.CanEnter(MovementDomain.Land, next))
                    queue.Enqueue((next, exit, steps + 1));
        }
        return null;
    }

    private static bool Escorted(GameState state, CombatUnit supply)
        => state.Armies.Any(u => u.Id != supply.Id && u.Pool.Active > 0 && !u.IsSupply
            && u.Field.Owner == supply.Field.Owner
            && u.Field.Position.Distance(supply.Field.Position) <= 2);
}
