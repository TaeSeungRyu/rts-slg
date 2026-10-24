namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class AiRuinPlanner(DeployService deployer, HexMap map, BalanceConfig balance)
{
    public GameState Plan(GameState state, FactionId faction)
    {
        if (state.Armies.Any(unit => unit.Field.Owner == faction
            && unit.AiRuinTarget is not null && unit.Pool.Active > 0)) return state;
        foreach (var candidate in RuinAiPolicy.Candidates(state, faction, map, balance))
        {
            var city = state.Cities.First(origin => origin.Id == candidate.Origin);
            var ruin = state.Ruins.First(target => target.Id == candidate.RuinId);
            var status = state.RuinStatus.First(entry => entry.RuinId == ruin.Id);
            var available = AiWarForcePolicy.Available(state, city);
            if (!RuinAiPolicy.ShouldAttack(ruin, status, faction, state.Day, available))
                continue;
            var required = status.Defenders * 2;
            var scratch = state;
            var deployed = 0;
            var maxUnit = CommandEfficiency.CommandTroopDeployLimit(
                state.ResearchOf(faction, FactionResearch.CommandTroopsCode), deployer.Balance);
            var egressDirection = DeploymentEgressRules.Directions.FirstOrDefault(direction =>
                DeploymentEgressRules.ExitGroup(city, direction).Contains(candidate.Exit));
            while (deployed < required)
            {
                var officers = scratch.GeneralsAt(city.Id)
                    .Where(id => !scratch.IsGeneralBusy(id) && !scratch.IsGeneralInField(id))
                    .Select(id => scratch.Generals.First(general => general.Id == id)).ToList();
                if (officers.Count == 0 || scratch.Cities.First(c => c.Id == city.Id).Provisions <= 0)
                    break;
                var shipCode = candidate.Domain == MovementDomain.DeepWater
                    ? scratch.PortShips.Where(stock => stock.City == city.Id && stock.Count > 0
                        && deployer.Troops.TryGetValue(stock.ShipCode, out var template)
                        && template.Class == TroopClass.Naval)
                        .OrderBy(stock => stock.ShipCode, StringComparer.Ordinal)
                        .Select(stock => stock.ShipCode).FirstOrDefault()
                    : null;
                var lines = scratch.Garrisons.Where(line => line.City == city.Id
                        && !line.Trainee && line.Troops >= 1000
                        && line.TrainingLevel >= deployer.Balance.DeployMinTraining
                        && deployer.Troops.TryGetValue(line.TroopCode, out var template)
                        && template.Class is not (TroopClass.Supply or TroopClass.Naval))
                    .OrderByDescending(line => line.Troops)
                    .ThenBy(line => line.TroopCode, StringComparer.Ordinal).ToList();
                var next = false;
                foreach (var line in lines)
                {
                    var troopClass = candidate.Domain == MovementDomain.DeepWater
                        ? TroopClass.Naval : deployer.Troops[line.TroopCode].Class;
                    var leader = officers.OrderByDescending(officer => officer.AptitudeFor(troopClass))
                        .ThenByDescending(officer => officer.Might)
                        .ThenBy(officer => officer.Id.Value).First();
                    var count = Math.Min(required - deployed, Math.Min(line.Troops,
                        candidate.Domain == MovementDomain.DeepWater
                            ? DeployService.ShipTroopCapacity : maxUnit));
                    if (count < 1000) continue;
                    var result = candidate.Domain == MovementDomain.DeepWater
                        ? shipCode is null ? CommandResult.Fail("선박이 부족합니다.", scratch)
                            : deployer.DeployNaval(scratch, new NavalDeployRequest(city.Id,
                                shipCode, line.TroopCode, count, leader.Id, Mode: UnitMode.Attack,
                                Target: ruin.Position, EgressDirection: egressDirection,
                                EgressExit: candidate.Exit))
                        : deployer.Deploy(scratch, new DeployRequest(city.Id, line.TroopCode,
                            count, leader.Id, Mode: UnitMode.Attack, Target: ruin.Position,
                            EgressDirection: egressDirection, EgressExit: candidate.Exit));
                    if (!result.Ok) continue;
                    var id = result.State.Armies[^1].Id;
                    scratch = result.State with { FieldArmies = result.State.Armies.Select(unit =>
                        unit.Id == id ? unit with { AiRuinTarget = ruin.Id } : unit).ToList() };
                    deployed += count;
                    next = true;
                    break;
                }
                if (!next) break;
            }
            if (deployed >= required) return scratch;
        }
        return state;
    }

    public static GameState Maintain(GameState state, FactionId faction)
    {
        return state with { FieldArmies = state.Armies.Select(unit =>
        {
            if (unit.Field.Owner != faction || unit.AiRuinTarget is not { } id) return unit;
            var ruin = state.Ruins.FirstOrDefault(target => target.Id == id);
            var status = state.RuinStatus.FirstOrDefault(entry => entry.RuinId == id);
            if (ruin is not null && status is not null && status.Defenders > 0
                && unit.Field.Mode == UnitMode.Attack
                && !status.IsProtected(state.Day) && !status.Registrations.Contains(faction))
                return unit;
            if (ruin is not null && status is not null && status.Defenders > 0
                && unit.Field.Mode != UnitMode.Attack
                && !status.IsProtected(state.Day) && !status.Registrations.Contains(faction))
                return unit with { AiRuinTarget = null };
            var home = state.Cities.Where(city => city.Owner == faction)
                .OrderBy(city => city.Id == unit.OriginCity ? 0 : 1)
                .ThenBy(city => city.Position.Distance(unit.Field.Position))
                .ThenBy(city => city.Id.Value).FirstOrDefault();
            return unit with { AiRuinTarget = null, Field = unit.Field with
            {
                Mode = home is null ? UnitMode.Standby : UnitMode.March,
                Target = home?.Position, ReturnCity = home?.Id,
                AssignedUnitTarget = null, PursuitTarget = null,
                ContinuousTarget = null, Waypoints = null, ContinuousWaypoints = null,
            } };
        }).ToList() };
    }
}
