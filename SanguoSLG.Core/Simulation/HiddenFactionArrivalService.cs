namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>예약된 연도가 되면 지정 거점 또는 가까운 대체 거점에서 세력을 등장시킨다.</summary>
public static class HiddenFactionArrivalService
{
    public static GameState Advance(GameState state)
    {
        if (state.HiddenFactionArrivals is not { Count: > 0 } arrivals) return state;
        var cities = state.Cities.ToList();
        var postings = state.Assignments.ToList();
        var garrisons = state.Garrisons.ToList();
        var pending = new List<HiddenFactionArrival>();
        foreach (var arrival in arrivals.OrderBy(entry => entry.Year).ThenBy(entry => entry.Faction.Value))
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
            // 지정 거점이 점령되어 있으면 가까운 중립 거점을 먼저 찾는다.
            // 중립 거점이 전혀 없다면 가장 가까운 거점에서 봉기한다.
            var destination = cities.Where(city => city.Owner.Value == 0)
                .OrderBy(city => city.Position.Distance(preferred.Position)).ThenBy(city => city.Id.Value)
                .FirstOrDefault() ?? cities.OrderBy(city => city.Position.Distance(preferred.Position))
                    .ThenBy(city => city.Id.Value).First();
            if (preferred.Owner.Value == 0) destination = preferred;
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
                Owner = arrival.Faction,
                Gold = Math.Max(3000, destination.Gold),
                Provisions = Math.Max(12000, destination.Provisions),
                Security = Math.Max(70, destination.Security),
            };
            garrisons.RemoveAll(force => force.City == destination.Id);
            garrisons.Add(new GarrisonForce(destination.Id, "swordsman", 10000, 50));
            postings = postings.Select(posting => posting.Faction == arrival.Faction
                ? posting with { Location = destination.Id } : posting).ToList();
        }
        return state with { Cities = cities, Postings = postings, GarrisonForces = garrisons,
            HiddenFactionArrivals = pending };
    }
}
