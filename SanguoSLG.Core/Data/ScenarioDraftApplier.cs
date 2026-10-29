namespace SanguoSLG.Core.Data;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public static class ScenarioDraftApplier
{
    public static Scenario Apply(Scenario source, ScenarioDraft draft, ScenarioMapPreset map)
    {
        var errors = draft.Validate(map, source.Generals.Select(g => g.Id.Value).ToHashSet());
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        var mapped = ScenarioMapPresetApplier.Apply(source, map);
        var citySetup = draft.Cities.ToDictionary(city => city.SiteId);
        return mapped with
        {
            Factions = draft.Factions.Select(entry => new Faction(new FactionId(entry.Id),
                entry.Name, new GeneralId(entry.RulerGeneralId), 0, entry.Color)).ToArray(),
            Cities = mapped.Cities.Select(city =>
            {
                var setup = citySetup[city.Id.Value];
                return city with
                {
                    Owner = new FactionId(setup.OwnerFactionId), Gold = setup.Gold,
                    Provisions = setup.Provisions, Wall = setup.Wall,
                    Security = setup.Security, Governor = null, Strategist = null,
                };
            }).ToArray(),
            Postings = draft.Generals.Select(entry => new GeneralPosting(new GeneralId(entry.GeneralId),
                new FactionId(entry.FactionId), entry.SiteId is { } id ? new CityId(id) : null)).ToArray(),
            InitialGarrisons = draft.Cities.SelectMany(city => city.Garrisons.Select(force =>
                new GarrisonForce(new CityId(city.SiteId), force.TroopCode, force.Troops, 50))).ToArray(),
            InitialShips = draft.Cities.SelectMany(city => city.Ships.Select(ship =>
                new PortShipStock(new CityId(city.SiteId), ship.ShipCode, ship.Count))).ToArray(),
        };
    }

    public static ScenarioDraft CreateSeed(string id, string title, Scenario source,
        ScenarioMapVersion mapVersion)
    {
        var map = mapVersion.Preset;
        var originalCities = source.Cities.ToDictionary(city => city.Name, StringComparer.Ordinal);
        var siteByName = map.Sites.Where(site => site.Kind is "city" or "port" or "gate")
            .ToDictionary(site => site.Name, StringComparer.Ordinal);
        var cities = siteByName.Values.OrderBy(site => site.Id).Select(site =>
        {
            originalCities.TryGetValue(site.Name, out var original);
            return new ScenarioCityEntry(site.Id, original?.Owner.Value ?? 0,
                original?.Gold ?? 0, original?.Provisions ?? 0, original?.Wall ?? 0,
                original?.Security ?? 70, [], []);
        }).ToArray();
        var firstOwned = cities.Where(city => city.OwnerFactionId > 0)
            .GroupBy(city => city.OwnerFactionId)
            .ToDictionary(group => group.Key, group => group.First().SiteId);
        var postings = source.PostingList.Where(posting => posting.Location is not null)
            .Select(posting =>
            {
                var oldCity = source.Cities.FirstOrDefault(city => city.Id == posting.Location);
                var siteId = oldCity is not null && siteByName.TryGetValue(oldCity.Name, out var site)
                    && cities.Any(city => city.SiteId == site.Id && city.OwnerFactionId == posting.Faction.Value)
                    ? site.Id : firstOwned.GetValueOrDefault(posting.Faction.Value);
                return new ScenarioGeneralEntry(posting.General.Value, posting.Faction.Value,
                    siteId == 0 ? null : siteId);
            }).ToList();
        foreach (var faction in source.Factions)
            if (!postings.Any(entry => entry.GeneralId == faction.Ruler.Value)
                && firstOwned.TryGetValue(faction.Id.Value, out var siteId))
                postings.Add(new ScenarioGeneralEntry(faction.Ruler.Value, faction.Id.Value, siteId));
        return new ScenarioDraft(1, id, title, "", 190, 1, 1,
            mapVersion.Id, mapVersion.Revision,
            source.Factions.Select(faction => new ScenarioFactionEntry(faction.Id.Value,
                faction.Name, faction.Ruler.Value, faction.Color)).ToArray(),
            cities, postings);
    }
}
