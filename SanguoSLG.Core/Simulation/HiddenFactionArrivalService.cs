namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>예약된 연도가 되면 지정 거점 또는 가까운 대체 거점에서 세력을 등장시킨다.</summary>
public static class HiddenFactionArrivalService
{
    public static GameState Advance(GameState state)
    {
        if (state.HiddenFactionArrivals is not { Count: > 0 } arrivals) return state;
        var cities = state.Cities.ToList();
        var factions = state.Factions.ToList();
        var postings = state.Assignments.ToList();
        var garrisons = state.Garrisons.ToList();
        var pending = new List<HiddenFactionArrival>();
        foreach (var arrival in arrivals.OrderBy(entry => entry.Year).ThenBy(entry => entry.Faction.Id.Value))
        {
            if (state.Year < arrival.Year)
            {
                pending.Add(arrival);
                continue;
            }
            var preferred = cities.FirstOrDefault(city => city.Id == arrival.PreferredCity);
            if (preferred is null)
            {
                pending.Add(arrival);
                continue;
            }
            // 지정 거점이 점령되었다면 8칸 안의 중립 거점을 먼저 찾는다.
            // 없을 때는 가까운 다른 거점에서 봉기하며, 고립된 거점이라면 지정 거점에서 봉기한다.
            var nearby = cities.Where(city => city.Id != preferred.Id
                    && city.Position.Distance(preferred.Position) <= 8)
                .OrderBy(city => city.Position.Distance(preferred.Position))
                .ThenBy(city => city.Id.Value).ToArray();
            var destination = preferred.Owner.Value == 0 ? preferred
                : nearby.FirstOrDefault(city => city.Owner.Value == 0)
                    ?? nearby.FirstOrDefault() ?? preferred;
            var formerOwner = destination.Owner;
            if (formerOwner.Value != 0)
            {
                var refuge = cities.Where(city => city.Id != destination.Id && city.Owner == formerOwner)
                    .OrderBy(city => city.Position.Distance(destination.Position))
                    .ThenBy(city => city.Id.Value).FirstOrDefault();
                postings = postings.Select(posting => posting.Location == destination.Id
                    && posting.Faction == formerOwner
                    ? posting with { Location = refuge?.Id } : posting).ToList();
            }
            cities[cities.FindIndex(city => city.Id == destination.Id)] = destination with
            {
                Owner = arrival.Faction.Id,
                Gold = Math.Max(3000, destination.Gold),
                Provisions = Math.Max(12000, destination.Provisions),
                Security = Math.Max(70, destination.Security),
            };
            garrisons.RemoveAll(force => force.City == destination.Id);
            garrisons.Add(new GarrisonForce(destination.Id, "swordsman", 10000, 50));
            factions.Add(arrival.Faction);
            postings.AddRange(arrival.Generals.Select(general => new GeneralPosting(general,
                arrival.Faction.Id, destination.Id)));
        }
        return state with { Factions = factions, Cities = cities, Postings = postings, GarrisonForces = garrisons,
            HiddenFactionArrivals = pending };
    }
}
