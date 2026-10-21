namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

/// <summary>
/// Phase 21 공세의 실제 출전 경로. 후보마다 독립된 불변 상태에서 전 부대를 편성하고
/// 보급·교전 검사가 모두 통과한 묶음만 원본에 반영한다.
/// </summary>
public sealed class AiOffensePlanner
{
    private readonly DeployService _deployer;
    private readonly AiConfig _config;
    private readonly CommandService? _commands;
    private readonly HexMap? _map;

    public AiOffensePlanner(DeployService deployer, AiConfig config,
        CommandService? commands = null, HexMap? map = null)
    {
        _deployer = deployer;
        _config = config;
        _commands = commands;
        _map = map;
    }

    public GameState Plan(GameState state, FactionId faction)
    {
        foreach (var origin in state.Cities.Where(c => c.Owner == faction && !c.IsPort
            && AiSortiePolicy.AllowsCombatSortie(c))
            .OrderBy(c => c.Id.Value))
        {
            var targets = state.Cities.Where(c => !c.IsPort && state.AreHostile(faction, c.Owner)
                    && state.IsScouted(faction, c.Id)
                    && c.Position.Distance(origin.Position) <= 15)
                .OrderBy(c => c.Position.Distance(origin.Position)).ThenBy(c => c.Id.Value);
            foreach (var target in targets)
            {
                if (state.Armies.Any(u => u.Field.Owner == faction && u.Field.Target == target.Position
                    && !u.IsSupply && !u.IsTransport))
                    continue;
                var officers = state.GeneralsAt(origin.Id)
                    .Where(id => !state.IsGeneralBusy(id))
                    .Select(id => state.Generals.First(g => g.Id == id))
                    .OrderBy(g => g.Id.Value).ToList();
                if (AiOffensiveCandidateService.Evaluate(state, origin, target).PassesForceGate
                    && officers.Count > _config.KeepGeneralsHome + 1)
                {
                    var preferred = AiOffensePatternService.Choose(state, origin, target,
                        _deployer.Troops, officers.Count);
                    var patterns = preferred.Pattern switch
                    {
                        AiOffensePattern.ArmyGroupSiege => new[] { AiOffensePattern.ArmyGroupSiege,
                            AiOffensePattern.RegularSiege, AiOffensePattern.CavalrySiege },
                        AiOffensePattern.CavalrySiege => new[] { AiOffensePattern.CavalrySiege,
                            AiOffensePattern.RegularSiege },
                        AiOffensePattern.RegularSiege => new[] { AiOffensePattern.RegularSiege },
                        _ => [],
                    };
                    foreach (var pattern in patterns)
                    {
                        var planned = TryBundle(state, origin, target, officers, pattern);
                        if (planned is null) continue;
                        state = planned;
                        break;
                    }
                }
                if (!state.Armies.Any(u => u.Field.Owner == faction && u.OriginCity == origin.Id
                    && u.Field.Target == target.Position && !u.IsSupply))
                {
                    var supporters = state.Cities.Where(c => c.Owner == faction && !c.IsPort
                        && AiSortiePolicy.AllowsCombatSortie(c)
                        && c.Id != origin.Id && c.Position.Distance(target.Position) <= 15)
                        .OrderBy(c => c.Position.Distance(target.Position)).ThenBy(c => c.Id.Value)
                        .ToList();
                    for (var count = 1; count <= supporters.Count; count++)
                    {
                        var coalition = TryCoalition(state, origin, supporters.Take(count).ToList(), target);
                        if (coalition is null) continue;
                        state = coalition;
                        break;
                    }
                }
                // 한 출발 성에서 한 주에 하나의 공세만 시작한다.
                if (state.Armies.Any(u => u.Field.Owner == faction && u.OriginCity == origin.Id
                    && u.Field.Target == target.Position && !u.IsSupply)) break;
            }
        }
        return state;
    }

    private GameState? TryBundle(GameState original, City origin, City target,
        IReadOnlyList<General> officers, AiOffensePattern pattern)
    {
        var route = RouteFor(original, origin, target);
        if (route is null) return null;
        var scratch = original;
        var available = AiOffensiveCandidateService.Evaluate(original, origin, target).AvailableTroops;
        var used = 0;
        var combatIds = new List<UnitId>();
        var maxCombat = target.Castle switch
        {
            CastleSize.Small => 3,
            CastleSize.Medium => 5,
            _ => 7,
        };
        var free = officers.ToList();
        var garrisons = original.Garrisons.Where(g => g.City == origin.Id && !g.Trainee
                && g.TrainingLevel >= _deployer.Balance.DeployMinTraining && g.Troops > 0
                && _deployer.Troops.ContainsKey(g.TroopCode))
            .OrderBy(g => g.TroopCode, StringComparer.Ordinal).ToList();

        bool DeployLine(GarrisonForce line)
        {
            var troop = _deployer.Troops[line.TroopCode];
            var remain = scratch.Garrisons.FirstOrDefault(g => g.City == origin.Id
                && !g.Trainee && g.TroopCode == line.TroopCode)?.Troops ?? 0;
            var count = Math.Min(Math.Min(remain, available - used),
                CommandEfficiency.CommandTroopDeployLimit(
                    scratch.ResearchOf(origin.Owner, FactionResearch.CommandTroopsCode), _deployer.Balance));
            if (count < Math.Min(1000, line.Troops) || free.Count <= _config.KeepGeneralsHome)
                return false;
            var leader = free.OrderByDescending(g => g.AptitudeFor(troop.Class))
                .ThenByDescending(g => g.Might).ThenBy(g => g.Id.Value).First();
            var result = _deployer.Deploy(scratch, new DeployRequest(origin.Id, line.TroopCode,
                count, leader.Id, Mode: UnitMode.Attack, Target: target.Position,
                EgressExit: route.Exit));
            if (!result.Ok) return false;
            scratch = result.State;
            free.Remove(leader);
            used += count;
            combatIds.Add(scratch.Armies[^1].Id);
            return true;
        }

        if (pattern == AiOffensePattern.ArmyGroupSiege)
        {
            if (free.Count < 4 || original.Garrisons.Where(g => g.City == origin.Id && !g.Trainee)
                    .Sum(g => g.Troops) < 50_000 || original.Garrisons.Where(g => g.City == target.Id)
                    .Sum(g => g.Troops) < 50_000)
                return null;
            var groupClasses = new[] { TroopClass.Infantry, TroopClass.Archer, TroopClass.Siege };
            var lines = new List<SupplyLine>();
            foreach (var troopClass in groupClasses)
            {
                var line = garrisons.Where(g => _deployer.Troops[g.TroopCode].Class == troopClass)
                    .OrderByDescending(g => g.Troops).FirstOrDefault();
                if (line is null || line.Troops < _deployer.Balance.ArmyGroupMinClassTroops)
                    return null;
                lines.Add(new SupplyLine(line.TroopCode, Math.Min(10_000, line.Troops)));
            }
            if (lines.Sum(l => l.Troops) > available - 1000) return null;
            var groupOfficers = free.OrderByDescending(g => AptitudeGrades.AverageFloor(
                    g.AptitudeFor(TroopClass.Infantry), g.AptitudeFor(TroopClass.Archer),
                    g.AptitudeFor(TroopClass.Siege)))
                .ThenByDescending(g => g.Might).ThenBy(g => g.Id.Value).ToList();
            var result = _deployer.DeployArmyGroup(scratch, new ArmyGroupDeployRequest(
                origin.Id, lines, groupOfficers[0].Id, groupOfficers[1].Id,
                UnitMode.Attack, target.Position, EgressExit: route.Exit));
            if (!result.Ok) return null;
            scratch = result.State;
            free.Remove(groupOfficers[0]);
            free.Remove(groupOfficers[1]);
            used += lines.Sum(l => l.Troops);
            combatIds.Add(scratch.Armies[^1].Id);
        }
        else
        {
            var siege = garrisons.Where(g => _deployer.Troops[g.TroopCode].Class == TroopClass.Siege)
                .OrderByDescending(g => _deployer.Troops[g.TroopCode].AtkBuilding)
                .ThenBy(g => g.TroopCode, StringComparer.Ordinal).FirstOrDefault();
            if (siege is null || !DeployLine(siege)) return null;
        }

        var support = garrisons.Where(g => _deployer.Troops[g.TroopCode].Class is not
                (TroopClass.Siege or TroopClass.Naval or TroopClass.Supply))
            .Where(g => pattern != AiOffensePattern.CavalrySiege
                || _deployer.Troops[g.TroopCode].Class == TroopClass.Cavalry)
            .OrderByDescending(g => free.Count == 0 ? AptitudeGrade.F : free.Max(o =>
                o.AptitudeFor(_deployer.Troops[g.TroopCode].Class)))
            .ThenByDescending(g => g.Troops).ThenBy(g => g.TroopCode, StringComparer.Ordinal).ToList();
        if (support.Count == 0) return null;
        foreach (var line in support)
        {
            while (combatIds.Count < maxCombat && available - used >= 2000
                && free.Count > _config.KeepGeneralsHome + (pattern == AiOffensePattern.CavalrySiege
                    && origin.Position.Distance(target.Position) < 15 ? 0 : 1))
            {
                if (!DeployLine(line)) break;
            }
        }
        if (combatIds.Count < 2) return null;

        var supplyRequired = pattern != AiOffensePattern.CavalrySiege
            || origin.Position.Distance(target.Position) >= 15;
        if (supplyRequired)
        {
            var reusable = ReusableSupply(original, origin, target);
            if (reusable is not null)
            {
                scratch = Follow(scratch, reusable.Id, combatIds[0]);
            }
            else
            {
            var supplyLine = scratch.Garrisons.Where(g => g.City == origin.Id && !g.Trainee
                    && g.TrainingLevel >= _deployer.Balance.DeployMinTraining
                    && g.Troops >= 1000 && available - used >= 1000)
                .OrderBy(g => g.TroopCode, StringComparer.Ordinal).FirstOrDefault();
            if (supplyLine is null || free.Count <= _config.KeepGeneralsHome) return null;
            var leader = free.OrderByDescending(g => g.AptitudeFor(TroopClass.Supply))
                .ThenBy(g => g.Id.Value).First();
            var result = _deployer.DeploySupply(scratch, new SupplyDeployRequest(origin.Id,
                [new SupplyLine(supplyLine.TroopCode, 1000)], leader.Id, UnitMode.March,
                target.Position, EgressExit: route.Exit));
            if (!result.Ok) return null;
            scratch = result.State;
            var supply = scratch.Armies[^1];
            scratch = Follow(scratch, supply.Id, combatIds[0]);
            used += 1000;
            }
        }
        if (used > available || !PassesProvisionBudget(scratch, target, combatIds)
            || !PassesSiegeTrial(original, scratch, target, combatIds))
            return null;
        scratch = MarkSiegeAssembly(scratch, target.Id, combatIds);
        return AttachDeclaration(original, scratch, target);
    }

    private GameState? TryCoalition(GameState original, City primary,
        IReadOnlyList<City> secondaries, City target)
    {
        var primaryRoute = RouteFor(original, primary, target);
        if (primaryRoute is null) return null;
        var secondaryRoutes = secondaries.ToDictionary(c => c.Id, c => RouteFor(original, c, target));
        if (secondaryRoutes.Values.Any(route => route is null)) return null;
        var primaryBudget = AiOffensiveCandidateService.Evaluate(original, primary, target,
            requiredRatioPercent: 0).AvailableTroops;
        var secondaryBudgets = secondaries.ToDictionary(c => c.Id, c =>
            AiOffensiveCandidateService.Evaluate(original, c, target,
                requiredRatioPercent: 0).AvailableTroops);
        var defenders = original.Garrisons.Where(g => g.City == target.Id).Sum(g => g.Troops);
        if (primaryBudget + secondaryBudgets.Values.Sum() < Math.Ceiling(defenders * 1.20)
            || primaryBudget < 2000 || secondaryBudgets.Values.Any(budget => budget < 1000))
            return null;

        var primaryOfficers = original.GeneralsAt(primary.Id)
            .Where(g => !original.IsGeneralBusy(g))
            .Select(g => original.Generals.First(x => x.Id == g)).ToList();
        var secondaryOfficers = secondaries.ToDictionary(c => c.Id, c => original.GeneralsAt(c.Id)
            .Where(g => !original.IsGeneralBusy(g))
            .Select(g => original.Generals.First(x => x.Id == g)).ToList());
        if (primaryOfficers.Count < _config.KeepGeneralsHome + 2
            || secondaryOfficers.Values.Any(officers => officers.Count < _config.KeepGeneralsHome + 1))
            return null;

        var primaryLines = original.Garrisons.Where(g => g.City == primary.Id && !g.Trainee
            && g.Troops > 0 && g.TrainingLevel >= _deployer.Balance.DeployMinTraining
            && _deployer.Troops.ContainsKey(g.TroopCode)).ToList();
        var siege = primaryLines.Where(g => _deployer.Troops[g.TroopCode].Class == TroopClass.Siege)
            .OrderByDescending(g => _deployer.Troops[g.TroopCode].AtkBuilding)
            .ThenBy(g => g.TroopCode, StringComparer.Ordinal).FirstOrDefault();
        if (siege is null) return null;

        var scratch = original;
        var usedPrimary = 0;
        var usedSecondary = secondaries.ToDictionary(c => c.Id, _ => 0);
        var combatIds = new List<UnitId>();
        var maxCombat = target.Castle switch
        {
            CastleSize.Small => 3,
            CastleSize.Medium => 5,
            _ => 7,
        };

        bool AddCombat(City city, GarrisonForce line, List<General> free,
            ref int spent, int budget)
        {
            if (free.Count <= _config.KeepGeneralsHome) return false;
            var remaining = scratch.Garrisons.FirstOrDefault(g => g.City == city.Id
                && !g.Trainee && g.TroopCode == line.TroopCode)?.Troops ?? 0;
            var troops = Math.Min(Math.Min(remaining, budget - spent),
                CommandEfficiency.CommandTroopDeployLimit(scratch.ResearchOf(city.Owner,
                    FactionResearch.CommandTroopsCode), _deployer.Balance));
            if (troops < 1000) return false;
            var troopClass = _deployer.Troops[line.TroopCode].Class;
            var leader = free.OrderByDescending(g => g.AptitudeFor(troopClass))
                .ThenByDescending(g => g.Might).ThenBy(g => g.Id.Value).First();
            var result = _deployer.Deploy(scratch, new DeployRequest(city.Id, line.TroopCode,
                troops, leader.Id, Mode: UnitMode.Attack, Target: target.Position,
                EgressExit: city.Id == primary.Id ? primaryRoute.Exit : secondaryRoutes[city.Id]!.Exit));
            if (!result.Ok) return false;
            scratch = result.State;
            free.Remove(leader);
            spent += troops;
            combatIds.Add(scratch.Armies[^1].Id);
            return true;
        }

        if (!AddCombat(primary, siege, primaryOfficers, ref usedPrimary, primaryBudget - 1000))
            return null;
        foreach (var secondary in secondaries)
        {
            var officers = secondaryOfficers[secondary.Id];
            var spent = usedSecondary[secondary.Id];
            var support = original.Garrisons.Where(g => g.City == secondary.Id && !g.Trainee
                    && g.Troops > 0 && g.TrainingLevel >= _deployer.Balance.DeployMinTraining
                    && _deployer.Troops.TryGetValue(g.TroopCode, out var troop)
                    && troop.Class is TroopClass.Infantry or TroopClass.Archer
                        or TroopClass.Cavalry or TroopClass.Elephant)
                .OrderByDescending(g => officers.Max(o => o.AptitudeFor(
                    _deployer.Troops[g.TroopCode].Class)))
                .ThenByDescending(g => g.Troops).ThenBy(g => g.TroopCode, StringComparer.Ordinal);
            foreach (var line in support)
            {
                while (combatIds.Count < maxCombat
                    && AddCombat(secondary, line, officers, ref spent,
                        secondaryBudgets[secondary.Id]))
                {
                }
            }
            usedSecondary[secondary.Id] = spent;
        }
        if (combatIds.Count < 2) return null;
        var reusable = ReusableSupply(original, primary, target);
        if (reusable is not null)
        {
            scratch = Follow(scratch, reusable.Id, combatIds[0]);
        }
        else
        {
        var supplyLine = scratch.Garrisons.Where(g => g.City == primary.Id && !g.Trainee
                && g.TrainingLevel >= _deployer.Balance.DeployMinTraining
                && g.Troops >= 1000 && primaryBudget - usedPrimary >= 1000)
            .OrderBy(g => g.TroopCode, StringComparer.Ordinal).FirstOrDefault();
        if (supplyLine is null || primaryOfficers.Count <= _config.KeepGeneralsHome)
            return null;
        var supplyLeader = primaryOfficers.OrderByDescending(g => g.AptitudeFor(TroopClass.Supply))
            .ThenBy(g => g.Id.Value).First();
        var supply = _deployer.DeploySupply(scratch, new SupplyDeployRequest(primary.Id,
            [new SupplyLine(supplyLine.TroopCode, 1000)], supplyLeader.Id,
            UnitMode.March, target.Position, EgressExit: primaryRoute.Exit));
        if (!supply.Ok) return null;
        scratch = supply.State;
        var supplyId = scratch.Armies[^1].Id;
        scratch = Follow(scratch, supplyId, combatIds[0]);
        }
        if (!PassesProvisionBudget(scratch, target, combatIds)
            || !PassesSiegeTrial(original, scratch, target, combatIds)) return null;
        scratch = MarkSiegeAssembly(scratch, target.Id, combatIds);
        return AttachDeclaration(original, scratch, target);
    }

    private static GameState MarkSiegeAssembly(GameState state, CityId target, IReadOnlyCollection<UnitId> combatIds)
    {
        var combat = combatIds.ToHashSet();
        return state with { FieldArmies = state.Armies.Select(unit =>
            combat.Contains(unit.Id) && unit.Class != TroopClass.Siege && !unit.IsArmyGroup
                ? unit with { AiSiegeAssemblyTarget = target }
                : unit).ToList() };
    }

    private static GameState Follow(GameState state, UnitId supply, UnitId combat) => state with
    {
        FieldArmies = state.Armies.Select(u => u.Id == supply
            ? u with { Field = u.Field with { FollowUnitId = combat, Target = null,
                Mode = UnitMode.March } } : u).ToList(),
    };

    private static CombatUnit? ReusableSupply(GameState state, City origin, City target)
        => state.Armies.Where(u => u.IsSupply && u.Field.Owner == origin.Owner
                && u.Field.FollowUnitId is null && u.Field.Target is null
                && u.Field.Position.Distance(origin.Position) <= 3
                && u.Provisions >= Math.Max(10, (origin.Position.Distance(target.Position) + 7)
                    * Math.Max(1, (u.Pool.Active + 999) / 1000)))
            .OrderBy(u => u.Id.Value).FirstOrDefault();

    private GameState AttachDeclaration(GameState original, GameState staged, City target)
    {
        if (_commands is null) return staged;
        foreach (var city in original.Cities.Where(c => c.Owner != target.Owner && !c.IsPort
            && staged.Armies.Any(u => u.OriginCity == c.Id && u.Field.Target == target.Position))
            .OrderBy(c => c.Position.Distance(target.Position)).ThenBy(c => c.Id.Value))
        {
            foreach (var officer in staged.GeneralsAt(city.Id).Where(g => !staged.IsGeneralBusy(g))
                .OrderBy(g => g.Value))
            {
                // 포고를 출전 이전 스냅숏에서 검증한다. 새 공격대가 아직 성 안에 있어도
                // 성 앞 4칸 배제 규칙이 포고를 부당하게 막지 않게 한다.
                var issued = _commands.Issue(original, new CommandRequest(city.Id,
                    CommandKind.DeclareWar, officer, TargetCity: target.Id));
                if (!issued.Ok) continue;
                return (staged with { PendingCommands = issued.State.Commands })
                    .ReleaseOfficerDuties(officer);
            }
        }
        // 포고는 공세 허가 조건이 아니다. 불가하면 점령 치안 손실을 감수한다.
        return staged;
    }

    private bool PassesProvisionBudget(GameState staged, City target,
        IReadOnlyList<UnitId> combatIds)
    {
        foreach (var unit in staged.Armies.Where(u => combatIds.Contains(u.Id) || u.IsSupply
            && u.Field.FollowUnitId is { } follow
            && combatIds.Contains(follow)))
        {
            if (unit.OriginCity is not { } cityId) return false;
            var origin = staged.Cities.FirstOrDefault(c => c.Id == cityId);
            if (origin is null) return false;
            var route = RouteFor(staged, origin, target);
            if (route is null) return false;
            var distance = route.Distance;
            var travelDays = (distance + Math.Max(1, unit.Field.Speed) - 1)
                / Math.Max(1, unit.Field.Speed);
            var daily = Math.Max(1, (unit.Pool.Active + 999) / 1000);
            // 목표 도달 + 최소 일주일의 포위. 자동보급은 아직 확보되지 않은 자원으로 계산하지 않는다.
            if (unit.Provisions < daily * (travelDays + 7)) return false;
        }
        return true;
    }

    private sealed record AiRoute(HexCoord? Exit, int Distance);

    private AiRoute? RouteFor(GameState state, City origin, City target)
    {
        if (_map is null) return new AiRoute(null, origin.Position.Distance(target.Position));
        var blocked = state.Buildings.Where(b => !b.IsExpired(state.Day)).Select(b => b.Position)
            .Concat(state.Ruins.Select(r => r.Position));
        var passability = new PassabilityMap(_map, [], state.Cities, blocked);
        var exits = DeploymentEgressRules.ExteriorTiles(origin)
            .Where(tile => passability.CanEnter(MovementDomain.Land, tile))
            .OrderBy(tile => tile.Distance(target.Position)).ThenBy(tile => tile.Q).ThenBy(tile => tile.R)
            .ToList();
        var approaches = DeploymentEgressRules.ExteriorTiles(target)
            .Where(tile => passability.CanEnter(MovementDomain.Land, tile)).ToHashSet();
        if (exits.Count == 0 || approaches.Count == 0) return null;
        var queue = new Queue<HexCoord>();
        var distances = new Dictionary<HexCoord, int>();
        var origins = new Dictionary<HexCoord, HexCoord>();
        foreach (var exit in exits)
        {
            queue.Enqueue(exit);
            distances[exit] = 0;
            origins[exit] = exit;
        }
        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();
            if (approaches.Contains(tile))
                return new AiRoute(origins[tile], distances[tile] + 2);
            if (distances[tile] >= 25) continue;
            foreach (var neighbor in tile.Neighbors())
            {
                if (distances.ContainsKey(neighbor)
                    || !passability.CanEnter(MovementDomain.Land, neighbor)) continue;
                distances[neighbor] = distances[tile] + 1;
                origins[neighbor] = origins[tile];
                queue.Enqueue(neighbor);
            }
        }
        return null;
    }

    private bool PassesSiegeTrial(GameState original, GameState staged, City target,
        IReadOnlyList<UnitId> combatIds)
    {
        var footprint = CastleFootprint.TilesFor(target).ToList();
        HexCoord SiegePosition(int range) => footprint
            .SelectMany(tile => tile.Neighbors().SelectMany(neighbor => neighbor.Neighbors()))
            .Distinct().Where(tile => footprint.Min(cityTile => cityTile.Distance(tile)) == range)
            .OrderBy(tile => tile.Distance(staged.Cities.First(c => c.Id == staged.Armies
                .First(u => combatIds.Contains(u.Id)).OriginCity).Position))
            .ThenBy(tile => tile.Q).ThenBy(tile => tile.R).First();
        var near = SiegePosition(1);
        var far = SiegePosition(2);
        var attackers = staged.Armies.Where(u => combatIds.Contains(u.Id))
            .Select(u => u with { AwaitingEgress = false,
                Field = u.Field with { Position = u.Field.RangeCastle >= 2 ? far : near,
                    Mode = UnitMode.Attack } }).ToList();
        var siege = new CampaignSiege(new BattleResolver(60), _deployer.Troops.Values.ToList());
        var cities = new List<City> { target };
        var garrisons = original.Garrisons.Where(g => g.City == target.Id).ToList();
        var initial = attackers.Sum(u => u.Pool.Active);
        for (var day = 0; day < 30; day++)
        {
            var outcome = siege.Resolve(attackers, cities, garrisons);
            attackers = outcome.Armies.Where(u => u.Pool.Active > 0).ToList();
            cities = outcome.Cities.ToList();
            garrisons = outcome.Garrisons.ToList();
            if (attackers.Count == 0 || attackers.Sum(u => u.Pool.Active) < initial / 2)
                return false;
            if (cities[0].Wall <= 0 && garrisons.Sum(g => g.Troops) <= 0)
                return true;
        }
        return false;
    }
}
