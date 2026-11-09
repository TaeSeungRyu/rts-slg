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
        var ships = state.PortShips.ToList();
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
            // 지정 거점이 점령되었다면 가까운 중립 거점만 사용한다. 기존 소유권은 빼앗지 않는다.
            var neutral = cities.Where(city => city.Id != preferred.Id && city.Owner.Value == 0)
                .OrderBy(city => city.Position.Distance(preferred.Position))
                .ThenBy(city => city.Id.Value).ToArray();
            var destination = preferred.Owner.Value == 0 ? preferred
                : neutral.FirstOrDefault();
            if (destination is null)
            {
                pending.Add(arrival);
                continue;
            }
            var configured = arrival.CitySetups ?? [];
            var preferredSetup = configured.FirstOrDefault(setup => setup.City == arrival.PreferredCity);
            var activated = new HashSet<CityId> { destination.Id };
            Restore(destination, preferredSetup);
            foreach (var setup in configured.Where(setup => setup.City != arrival.PreferredCity))
            {
                var city = cities.FirstOrDefault(candidate => candidate.Id == setup.City);
                if (city is null || city.Owner.Value != 0) continue;
                activated.Add(city.Id);
                Restore(city, setup);
            }
            if (preferredSetup is null)
                garrisons.Add(new GarrisonForce(destination.Id, "swordsman", 10000, 50));
            if (factions.All(faction => faction.Id != arrival.Faction.Id)) factions.Add(arrival.Faction);
            var plannedPostings = arrival.GeneralPostings ?? arrival.Generals.Select(general =>
                new GeneralPosting(general, arrival.Faction.Id, null)).ToArray();
            postings.RemoveAll(posting => posting.Faction == arrival.Faction.Id);
            postings.AddRange(plannedPostings.Select(posting => posting with
            {
                Location = posting.Location is { } location && activated.Contains(location)
                    ? location : destination.Id,
            }));

            void Restore(City city, HiddenFactionCitySetup? setup)
            {
                cities[cities.FindIndex(candidate => candidate.Id == city.Id)] = city with
                {
                    Owner = arrival.Faction.Id,
                    Gold = setup?.Gold ?? Math.Max(3000, city.Gold),
                    Provisions = setup?.Provisions ?? Math.Max(12000, city.Provisions),
                    Security = setup?.Security ?? Math.Max(70, city.Security),
                };
                garrisons.RemoveAll(force => force.City == city.Id);
                if (setup is not null) garrisons.AddRange(setup.Garrisons.Select(force => force with { City = city.Id }));
                ships.RemoveAll(ship => ship.City == city.Id);
                if (setup is not null) ships.AddRange(setup.Ships.Select(ship => ship with { City = city.Id }));
            }
        }
        return state with { Factions = factions, Cities = cities, Postings = postings, GarrisonForces = garrisons,
            PortShipStocks = ships, HiddenFactionArrivals = pending };
    }
}
