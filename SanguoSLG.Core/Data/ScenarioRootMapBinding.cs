namespace SanguoSLG.Core.Data;

public static class ScenarioRootMapBinding
{
    public static bool UsesRoot(string mapId)
        => mapId is "default-east-asia" or "legacy-east-asia" or "edited-east-asia";

    public static ScenarioDraft Bind(ScenarioDraft draft, ScenarioMapVersion root,
        ScenarioMapPreset? previous = null)
    {
        if (!UsesRoot(draft.MapId)) return draft;
        if (root.Id != "default-east-asia")
            throw new InvalidDataException("기본 지도 ID가 올바르지 않습니다.");
        var sites = root.Preset.Sites.Where(site => site.Kind is "city" or "port" or "gate")
            .ToDictionary(site => site.Id);
        var existing = draft.Cities.ToDictionary(city => city.SiteId);
        var cities = sites.Values.Select(site => existing.TryGetValue(site.Id, out var city)
            ? city : new ScenarioCityEntry(site.Id, 0, 0, 0, 0, 70, [], []))
            .ToDictionary(city => city.SiteId);
        var oldSites = previous?.Sites.ToDictionary(site => site.Id) ?? [];
        var relocated = new Dictionary<int, int>();
        foreach (var lost in draft.Cities.Where(city => city.OwnerFactionId > 0
            && !sites.ContainsKey(city.SiteId)).OrderBy(city => city.SiteId))
        {
            if (!oldSites.TryGetValue(lost.SiteId, out var origin))
                throw new InvalidDataException($"삭제된 거점 {lost.SiteId}의 이전 위치를 찾을 수 없습니다.");
            var candidates = sites.Values.Where(site => lost.Ships.Count == 0 || site.Kind == "port")
                .OrderBy(site => cities[site.Id].OwnerFactionId == 0 ? 0
                    : cities[site.Id].OwnerFactionId == lost.OwnerFactionId ? 1 : 2)
                .ThenBy(site => Math.Abs(site.Column - origin.Column) + Math.Abs(site.Row - origin.Row))
                .ThenBy(site => site.Id).ToArray();
            var target = candidates.FirstOrDefault(site => cities[site.Id].OwnerFactionId is 0
                || cities[site.Id].OwnerFactionId == lost.OwnerFactionId)
                ?? throw new InvalidDataException($"{origin.Name}을(를) 옮길 빈 거점이 없습니다.");
            var current = cities[target.Id];
            cities[target.Id] = current with
            {
                OwnerFactionId = lost.OwnerFactionId,
                Gold = current.Gold + lost.Gold,
                Provisions = current.Provisions + lost.Provisions,
                Wall = Math.Max(current.Wall, lost.Wall),
                Security = Math.Max(current.Security, lost.Security),
                Garrisons = current.Garrisons.Concat(lost.Garrisons)
                    .GroupBy(force => force.TroopCode)
                    .Select(group => new ScenarioGarrisonEntry(group.Key, group.Sum(force => force.Troops)))
                    .ToArray(),
                Ships = current.Ships.Concat(lost.Ships)
                    .GroupBy(ship => ship.ShipCode)
                    .Select(group => new ScenarioShipEntry(group.Key, group.Sum(ship => ship.Count)))
                    .ToArray(),
            };
            relocated[lost.SiteId] = target.Id;
        }
        var generals = draft.Generals.Select(general =>
        {
            if (general.SiteId is not { } oldId || sites.ContainsKey(oldId)) return general;
            if (relocated.TryGetValue(oldId, out var moved)) return general with { SiteId = moved };
            if (!oldSites.TryGetValue(oldId, out var origin))
                throw new InvalidDataException($"장수 {general.GeneralId}의 이전 거점 {oldId}을(를) 찾을 수 없습니다.");
            var target = sites.Values.Where(site => cities[site.Id].OwnerFactionId == general.FactionId)
                .OrderBy(site => Math.Abs(site.Column - origin.Column) + Math.Abs(site.Row - origin.Row))
                .ThenBy(site => site.Id).FirstOrDefault()
                ?? throw new InvalidDataException($"장수 {general.GeneralId}의 새 주둔 거점이 없습니다.");
            return general with { SiteId = target.Id };
        }).ToArray();
        var factions = draft.Factions.Select(faction =>
        {
            if (!faction.Hidden || sites.ContainsKey(faction.ArrivalSiteId)) return faction;
            if (!oldSites.TryGetValue(faction.ArrivalSiteId, out var old))
                throw new InvalidDataException($"숨은세력 {faction.Name}의 이전 출현 거점을 찾을 수 없습니다.");
            var target = sites.Values.OrderBy(site => Math.Abs(site.Column - old.Column)
                + Math.Abs(site.Row - old.Row)).ThenBy(site => site.Id).First();
            return faction with { ArrivalSiteId = target.Id };
        }).ToArray();
        return draft with { MapId = root.Id, MapRevision = root.Revision,
            Cities = cities.Values.OrderBy(city => city.SiteId).ToArray(), Generals = generals,
            Factions = factions };
    }
}
