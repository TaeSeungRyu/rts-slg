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
    private readonly BalanceConfig? _economy;

    public AiOffensePlanner(DeployService deployer, AiConfig config,
        CommandService? commands = null, HexMap? map = null, BalanceConfig? economy = null)
    {
        _deployer = deployer;
        _config = config;
        _commands = commands;
        _map = map;
        _economy = economy;
    }

    public GameState Plan(GameState state, FactionId faction)
    {
        foreach (var origin in state.Cities.Where(c => c.Owner == faction && !c.IsPort
            && AiSortiePolicy.AllowsCombatSortie(c, _economy))
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
                {
                    state = TryReinforceExistingSiege(state, origin, target) ?? state;
                    continue;
                }
                var officers = state.GeneralsAt(origin.Id)
                    .Where(id => !state.IsGeneralBusy(id))
                    .Select(id => state.Generals.First(g => g.Id == id))
                    .OrderBy(g => g.Id.Value).ToList();
                var force = AiOffensiveCandidateService.Evaluate(state, origin, target);
                var canPressure = state.Day - origin.AiLastPressureDay >= 21
                    && AiOffensiveCandidateService.Evaluate(state, origin, target,
                        requiredRatioPercent: 70).PassesForceGate
                    && state.Garrisons.Any(g => g.City == origin.Id && !g.Trainee
                        && g.Troops >= 1000 && _deployer.Troops.TryGetValue(g.TroopCode, out var troop)
                        && troop.Class == TroopClass.Siege && troop.RangeCastle >= 2);
                if (force.PassesForceGate && officers.Count > _config.KeepGeneralsHome + 1)
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
                if (canPressure && officers.Count > _config.KeepGeneralsHome + 1
                    && !state.Armies.Any(unit => unit.Field.Owner == faction
                        && unit.OriginCity == origin.Id && unit.Field.Target == target.Position
                        && !unit.IsSupply))
                {
                    state = TryBundle(state, origin, target, officers,
                        AiOffensePattern.RegularSiege, pressure: true) ?? state;
                }
                if (!state.Armies.Any(u => u.Field.Owner == faction && u.OriginCity == origin.Id
                    && u.Field.Target == target.Position && !u.IsSupply))
                {
                    var supporters = state.Cities.Where(c => c.Owner == faction && !c.IsPort
                        && AiSortiePolicy.AllowsCombatSortie(c, _economy)
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

    private GameState? TryReinforceExistingSiege(GameState state, City origin, City target)
    {
        var siege = state.Armies.Where(unit => unit.Field.Owner == origin.Owner
                && unit.Class == TroopClass.Siege && unit.Pool.Active > 0
                && !unit.IsWaitingDeployment && !unit.IsWaitingEgress
                && unit.OriginCity != origin.Id && unit.Field.Target == target.Position
                && unit.AiPressureUntilDay > state.Day
                && unit.Field.Position.Distance(target.Position) <= 4)
            .OrderBy(unit => unit.Id.Value).FirstOrDefault();
        if (siege is null || state.Armies.Any(unit => unit.Field.Owner == origin.Owner
                && unit.OriginCity == origin.Id && unit.AiSiegeAssemblyTarget == target.Id))
            return null;
        var attacking = state.Armies.Count(unit => unit.Field.Owner == origin.Owner
            && unit.AiSiegeAssemblyTarget == target.Id);
        var limit = target.Castle switch
        {
            CastleSize.Small => 3,
            CastleSize.Medium => 5,
            _ => 7,
        };
        if (attacking >= limit) return null;
        var route = RouteFor(state, origin, target);
        if (route is null) return null;
        var officers = state.GeneralsAt(origin.Id).Where(id => !state.IsGeneralBusy(id))
            .Select(id => state.Generals.First(g => g.Id == id)).ToList();
        if (officers.Count <= _config.KeepGeneralsHome) return null;
        var available = AiOffensiveCandidateService.Evaluate(state, origin, target,
            requiredRatioPercent: 0).AvailableTroops;
        var line = state.Garrisons.Where(g => g.City == origin.Id && !g.Trainee
                && g.TrainingLevel >= _deployer.Balance.DeployMinTraining && g.Troops >= 1000
                && _deployer.Troops.TryGetValue(g.TroopCode, out var troop)
                && troop.Class is TroopClass.Infantry or TroopClass.Archer
                    or TroopClass.Cavalry or TroopClass.Elephant)
            .OrderByDescending(g => officers.Max(officer => officer.AptitudeFor(
                _deployer.Troops[g.TroopCode].Class)))
            .ThenByDescending(g => g.Troops).ThenBy(g => g.TroopCode, StringComparer.Ordinal)
            .FirstOrDefault();
        if (line is null) return null;
        var count = Math.Min(Math.Min(line.Troops, available),
            CommandEfficiency.CommandTroopDeployLimit(state.ResearchOf(origin.Owner,
                FactionResearch.CommandTroopsCode), _deployer.Balance));
        if (count < 1000) return null;
        var template = _deployer.Troops[line.TroopCode];
        var travelDays = (route.Distance + Math.Max(1, template.MovementPerDay) - 1)
            / Math.Max(1, template.MovementPerDay);
        if (state.Day + travelDays + 3 >= siege.AiPressureUntilDay) return null;
        var leader = officers.OrderByDescending(officer => officer.AptitudeFor(template.Class))
            .ThenByDescending(officer => officer.Might).ThenBy(officer => officer.Id.Value).First();
        var result = _deployer.Deploy(state, new DeployRequest(origin.Id, line.TroopCode,
            count, leader.Id, Mode: UnitMode.Attack, Target: target.Position,
            EgressExit: route.Exit));
        if (!result.Ok) return null;
        var reinforcement = result.State.Armies[^1];
        var daily = Math.Max(1, (count + 999) / 1000);
        if (reinforcement.Provisions < daily * (travelDays * 2 + 3)) return null;
        var escort = target.Wall > 0 ? SiegePosition(result.State, target, [siege.Id], 2)
            : target.Position;
        return result.State with { FieldArmies = result.State.Armies.Select(unit =>
            unit.Id == reinforcement.Id ? unit with
            {
                AiSiegeAssemblyTarget = target.Id,
                AiPressureUntilDay = siege.AiPressureUntilDay,
                Field = unit.Field with { Mode = target.Wall > 0 ? UnitMode.March : UnitMode.Attack,
                    Target = escort, ContinuousTarget = null },
            } : unit).ToList() };
    }

    private GameState? TryBundle(GameState original, City origin, City target,
        IReadOnlyList<General> officers, AiOffensePattern pattern, bool pressure = false)
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
            var siege = garrisons.Where(g => _deployer.Troops[g.TroopCode].Class == TroopClass.Siege
                    && (!pressure || _deployer.Troops[g.TroopCode].RangeCastle >= 2))
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

        var supplyRequired = pressure
            ? route.Distance >= 8
            : pattern != AiOffensePattern.CavalrySiege
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
            || !PassesSiegeTrial(original, scratch, target, combatIds, pressure))
            return null;
        scratch = ScheduleSiegeAssembly(scratch, target, combatIds);
        if (pressure)
        {
            var escortPost = SiegePosition(scratch, target, combatIds, 2);
            var travelDays = scratch.Armies.Where(unit => combatIds.Contains(unit.Id))
                .Select(unit => (Math.Max(0, route.Distance - unit.Field.RangeCastle)
                    + Math.Max(1, unit.Field.Speed) - 1) / Math.Max(1, unit.Field.Speed))
                .DefaultIfEmpty(0).Max();
            scratch = scratch with
            {
                FieldArmies = scratch.Armies.Select(unit => combatIds.Contains(unit.Id)
                    ? unit with
                    {
                        AiPressureUntilDay = original.Day + travelDays + 14,
                        AiOpeningAttack = unit.Class == TroopClass.Siege && unit.AiOpeningAttack,
                        Field = unit.Class == TroopClass.Siege ? unit.Field
                            : unit.Field with { Mode = UnitMode.March, Target = escortPost,
                                ContinuousTarget = null },
                    } : unit).ToList(),
                Cities = scratch.Cities.Select(city => city.Id == origin.Id
                    ? city with { AiLastPressureDay = original.Day } : city).ToList(),
            };
        }
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
        scratch = ScheduleSiegeAssembly(scratch, target, combatIds);
        return AttachDeclaration(original, scratch, target);
    }

    private GameState ScheduleSiegeAssembly(GameState state, City target,
        IReadOnlyCollection<UnitId> combatIds)
    {
        var combat = combatIds.ToHashSet();
        int TravelDays(CombatUnit unit)
        {
            if (unit.OriginCity is not { } originId) return 0;
            var origin = state.Cities.First(city => city.Id == originId);
            var distance = RouteFor(state, origin, target)?.Distance
                ?? origin.Position.Distance(target.Position);
            var approach = Math.Max(0, distance - Math.Max(1, unit.Field.RangeCastle));
            return (approach + Math.Max(1, unit.Field.Speed) - 1)
                / Math.Max(1, unit.Field.Speed);
        }
        var siegeArrival = state.Armies.Where(unit => combat.Contains(unit.Id)
                && (unit.Class == TroopClass.Siege || unit.IsArmyGroup))
            .Select(TravelDays).DefaultIfEmpty(0).Max();
        return state with { FieldArmies = state.Armies.Select(unit =>
            combat.Contains(unit.Id) && unit.Class != TroopClass.Siege && !unit.IsArmyGroup
                ? unit with { AiSiegeAssemblyTarget = target.Id,
                    DeploymentDelayDays = Math.Min(DeployService.MaxDeploymentDelayDays,
                        Math.Max(0, siegeArrival - TravelDays(unit))) }
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
        var shortage = 0L;
        var supplySurplus = 0L;
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
            var campaignDays = travelDays * 2 + 14;
            var remaining = (long)unit.Provisions - (long)daily * campaignDays;
            if (unit.IsSupply) supplySurplus += Math.Max(0, remaining);
            else shortage += Math.Max(0, -remaining);
            if (unit.IsSupply && remaining < 0) return false;
        }
        return shortage <= supplySurplus;
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
        IReadOnlyList<UnitId> combatIds, bool pressure = false)
    {
        var near = SiegePosition(staged, target, combatIds, 1);
        var far = SiegePosition(staged, target, combatIds, 2);
        var attackers = staged.Armies.Where(u => combatIds.Contains(u.Id))
            .Select(u => u with { AwaitingEgress = false,
                Field = u.Field with { Position = pressure || u.Field.RangeCastle >= 2 ? far : near,
                    Mode = UnitMode.Attack } }).ToList();
        var siege = new CampaignSiege(new BattleResolver(60), _deployer.Troops.Values.ToList());
        var cities = new List<City> { target };
        var garrisons = original.Garrisons.Where(g => g.City == target.Id).ToList();
        var initial = attackers.Sum(u => u.Pool.Active);
        var initialSiege = attackers.Where(u => u.Class == TroopClass.Siege)
            .Sum(u => u.Pool.Active);
        var initialWall = target.Wall;
        var initialDefenders = garrisons.Sum(g => g.Troops);
        for (var day = 0; day < (pressure ? 7 : 30); day++)
        {
            var outcome = siege.Resolve(attackers, cities, garrisons);
            attackers = outcome.Armies.Where(u => u.Pool.Active > 0).ToList();
            cities = outcome.Cities.ToList();
            garrisons = outcome.Garrisons.ToList();
            if (attackers.Count == 0 || attackers.Sum(u => u.Pool.Active) < initial * (pressure ? 7 : 5) / 10)
                return false;
            if (cities[0].Wall <= 0 && garrisons.Sum(g => g.Troops) <= 0)
                return true;
        }
        return pressure && attackers.Where(u => u.Class == TroopClass.Siege)
            .Sum(u => u.Pool.Active) >= initialSiege / 2
            && (cities[0].Wall < initialWall
            || garrisons.Sum(g => g.Troops) < initialDefenders);
    }

    private static HexCoord SiegePosition(GameState staged, City target,
        IReadOnlyList<UnitId> combatIds, int range)
    {
        var footprint = CastleFootprint.TilesFor(target).ToList();
        var originId = staged.Armies.First(u => combatIds.Contains(u.Id)).OriginCity;
        var origin = staged.Cities.First(c => c.Id == originId);
        return footprint.SelectMany(tile => tile.Neighbors()
                .SelectMany(neighbor => neighbor.Neighbors()))
            .Distinct().Where(tile => footprint.Min(cityTile => cityTile.Distance(tile)) == range)
            .OrderBy(tile => tile.Distance(origin.Position))
            .ThenBy(tile => tile.Q).ThenBy(tile => tile.R).First();
    }
}
