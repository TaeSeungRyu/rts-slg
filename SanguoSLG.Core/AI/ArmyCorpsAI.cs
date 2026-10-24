namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

/// <summary>플레이어 군단에 기존 세력 AI를 적용하되 맡기지 않은 거점과 부대는 격리한다.</summary>
public sealed class ArmyCorpsAI(FactionAI factionAI)
{
    public GameState PlanWeek(GameState state, FactionId player)
    {
        foreach (var corps in state.Corps.Where(entry => entry.Faction == player)
            .OrderBy(entry => entry.Id).ToList())
        {
            var assigned = corps.Cities.Where(id => state.Cities.Any(city => city.Id == id
                && city.Owner == player)).ToHashSet();
            if (assigned.Count == 0) continue;
            var hiddenCities = state.Cities.Where(city => city.Owner == player
                && !assigned.Contains(city.Id)).Select(city => city.Id).ToHashSet();
            var proxy = new FactionId(1_000_000 + corps.Id);
            var hiddenArmies = state.Armies.Where(unit => unit.Field.Owner == player
                && (unit.OriginCity is not { } origin || !assigned.Contains(origin)))
                .Select(unit => unit.Id).ToHashSet();
            var originalFaction = state.Factions.FirstOrDefault(entry => entry.Id == player);
            if (originalFaction is null) continue;
            var projected = state with
            {
                Cities = state.Cities.Select(city => hiddenCities.Contains(city.Id)
                    ? city with { Owner = proxy } : city).ToList(),
                FieldArmies = state.Armies.Select(unit => hiddenArmies.Contains(unit.Id)
                    ? unit with { Field = unit.Field with { Owner = proxy } } : unit).ToList(),
                FactionAlliances = state.Alliances.Append(FactionAlliance.Create(player, proxy,
                    state.Day)).ToList(),
                Factions = state.Factions.Select(entry => entry.Id == player
                    ? entry with { AiNextWarDecisionDay = corps.AiNextWarDecisionDay }
                    : entry).ToList(),
            };
            var planned = factionAI.PlanWeek(projected, player,
                allowOffense: corps.Policy == ArmyCorpsPolicy.Combat);
            var nextClock = planned.Factions.First(entry => entry.Id == player).AiNextWarDecisionDay;
            state = planned with
            {
                Cities = planned.Cities.Select(city => hiddenCities.Contains(city.Id)
                    ? city with { Owner = player } : city).ToList(),
                FieldArmies = planned.Armies.Select(unit => hiddenArmies.Contains(unit.Id)
                    ? unit with { Field = unit.Field with { Owner = player } } : unit).ToList(),
                FactionAlliances = state.Alliances,
                Factions = planned.Factions.Select(entry => entry.Id == player
                    ? entry with { AiNextWarDecisionDay = originalFaction.AiNextWarDecisionDay }
                    : entry).ToList(),
                ArmyCorpsList = state.Corps.Select(entry => entry.Id == corps.Id
                    ? entry with { AiNextWarDecisionDay = nextClock } : entry).ToList(),
            };
        }
        return state;
    }
}
