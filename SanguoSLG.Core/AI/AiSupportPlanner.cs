namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

/// <summary>실제로 공격받는 성에 가장 빨리 닿을 수 있는 한 성에서 지원대를 출전시킨다.</summary>
public sealed class AiSupportPlanner(DeployService deployer, HexMap map, BalanceConfig balance)
{
    public GameState Plan(GameState state, FactionId faction)
    {
        foreach (var defended in state.Cities.Where(city => city.Owner == faction && !city.IsPort)
                     .OrderBy(city => city.Id.Value))
        {
            var attackers = AiSupportRoutePolicy.Attackers(state, defended);
            if (attackers.Count == 0 || state.Armies.Any(unit => unit.Field.Owner == faction
                    && unit.AiSupportDefendedCity == defended.Id && unit.Pool.Active > 0))
                continue;

            var excluded = new HashSet<CityId>();
            while (true)
            {
                var route = AiSupportRoutePolicy.Closest(state, defended, map, deployer.Troops,
                    balance, deployer.Balance.DeployMinTraining, excluded);
                if (route is null) break;
                excluded.Add(route.Origin);
                var origin = state.Cities.First(city => city.Id == route.Origin);
                var line = state.Garrisons.First(garrison => garrison.City == origin.Id
                    && garrison.TroopCode == route.TroopCode && !garrison.Trainee);
                var total = state.Garrisons.Where(garrison => garrison.City == origin.Id
                    && !garrison.Trainee).Sum(garrison => garrison.Troops);
                var count = Math.Min(line.Troops, Math.Min(total * 40 / 100,
                    CommandEfficiency.CommandTroopDeployLimit(
                        state.ResearchOf(faction, FactionResearch.CommandTroopsCode),
                        deployer.Balance)));
                if (count < 6000) continue;
                var troopClass = deployer.Troops[route.TroopCode].Class;
                var officer = state.GeneralsAt(origin.Id)
                    .Where(id => !state.IsGeneralBusy(id) && !state.IsGeneralInField(id))
                    .Select(id => state.Generals.First(general => general.Id == id))
                    .OrderByDescending(general => general.AptitudeFor(troopClass))
                    .ThenByDescending(general => general.Might)
                    .ThenBy(general => general.Id.Value).FirstOrDefault();
                if (officer is null) continue;
                var attacker = attackers.OrderBy(unit => unit.Field.Position.Distance(defended.Position))
                    .ThenBy(unit => unit.Id.Value).First();
                var result = deployer.Deploy(state, new DeployRequest(origin.Id, route.TroopCode,
                    count, officer.Id, Mode: UnitMode.Attack,
                    Target: attacker.Field.Position, EgressExit: route.Exit));
                if (!result.Ok) continue;
                var deployedId = result.State.Armies[^1].Id;
                state = result.State with { FieldArmies = result.State.Armies.Select(unit =>
                    unit.Id == deployedId ? unit with { AiSupportDefendedCity = defended.Id,
                        Field = unit.Field with { AssignedUnitTarget = attacker.Id,
                            ReturnCity = origin.Id } } : unit).ToList() };
                break;
            }
        }
        return state;
    }
}
