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

    public AiOffensePlanner(DeployService deployer, AiConfig config)
    {
        _deployer = deployer;
        _config = config;
    }

    public GameState Plan(GameState state, FactionId faction)
    {
        foreach (var origin in state.Cities.Where(c => c.Owner == faction && !c.IsPort)
            .OrderBy(c => c.Id.Value))
        {
            var targets = state.Cities.Where(c => !c.IsPort && state.AreHostile(faction, c.Owner)
                    && c.Position.Distance(origin.Position) <= 15)
                .OrderBy(c => c.Position.Distance(origin.Position)).ThenBy(c => c.Id.Value);
            foreach (var target in targets)
            {
                if (state.Armies.Any(u => u.Field.Owner == faction && u.Field.Target == target.Position
                    && !u.IsSupply && !u.IsTransport))
                    continue;
                if (!AiOffensiveCandidateService.Evaluate(state, origin, target).PassesForceGate)
                    continue;

                var officers = state.GeneralsAt(origin.Id)
                    .Where(id => !state.IsGeneralBusy(id))
                    .Select(id => state.Generals.First(g => g.Id == id))
                    .OrderBy(g => g.Id.Value).ToList();
                if (officers.Count <= _config.KeepGeneralsHome + 1)
                    continue;

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
                count, leader.Id, Mode: UnitMode.Attack, Target: target.Position));
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
                UnitMode.Attack, target.Position));
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
            var supplyLine = scratch.Garrisons.Where(g => g.City == origin.Id && !g.Trainee
                    && g.TrainingLevel >= _deployer.Balance.DeployMinTraining
                    && g.Troops >= 1000 && available - used >= 1000)
                .OrderBy(g => g.TroopCode, StringComparer.Ordinal).FirstOrDefault();
            if (supplyLine is null || free.Count <= _config.KeepGeneralsHome) return null;
            var leader = free.OrderByDescending(g => g.AptitudeFor(TroopClass.Supply))
                .ThenBy(g => g.Id.Value).First();
            var result = _deployer.DeploySupply(scratch, new SupplyDeployRequest(origin.Id,
                [new SupplyLine(supplyLine.TroopCode, 1000)], leader.Id, UnitMode.March,
                target.Position));
            if (!result.Ok) return null;
            scratch = result.State;
            var supply = scratch.Armies[^1];
            scratch = scratch with { FieldArmies = scratch.Armies.Select(u => u.Id == supply.Id
                ? u with { Field = u.Field with { FollowUnitId = combatIds[0], Target = null } }
                : u).ToList() };
            used += 1000;
        }
        if (used > available || !PassesProvisionBudget(scratch, origin, target, combatIds)
            || !PassesSiegeTrial(original, scratch, target, combatIds))
            return null;
        return scratch;
    }

    private static bool PassesProvisionBudget(GameState staged, City origin, City target,
        IReadOnlyList<UnitId> combatIds)
    {
        var distance = origin.Position.Distance(target.Position);
        foreach (var unit in staged.Armies.Where(u => combatIds.Contains(u.Id) || u.IsSupply
            && u.OriginCity == origin.Id && u.Field.FollowUnitId is { } follow
            && combatIds.Contains(follow)))
        {
            var travelDays = (distance + Math.Max(1, unit.Field.Speed) - 1)
                / Math.Max(1, unit.Field.Speed);
            var daily = Math.Max(1, (unit.Pool.Active + 999) / 1000);
            // 목표 도달 + 최소 일주일의 포위. 자동보급은 아직 확보되지 않은 자원으로 계산하지 않는다.
            if (unit.Provisions < daily * (travelDays + 7)) return false;
        }
        return true;
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
