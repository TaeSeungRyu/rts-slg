namespace SanguoSLG.Core.Data;

public static class ScenarioRootMapBinding
{
    public static bool UsesRoot(string mapId)
        => mapId is "default-east-asia" or "legacy-east-asia" or "edited-east-asia";

    public static ScenarioDraft Bind(ScenarioDraft draft, ScenarioMapVersion root)
    {
        if (!UsesRoot(draft.MapId)) return draft;
        if (root.Id != "default-east-asia")
            throw new InvalidDataException("기본 지도 ID가 올바르지 않습니다.");
        var sites = root.Preset.Sites.Where(site => site.Kind is "city" or "port" or "gate")
            .ToDictionary(site => site.Id);
        var missing = draft.Cities.Where(city => city.OwnerFactionId > 0 && !sites.ContainsKey(city.SiteId))
            .Select(city => city.SiteId)
            .Concat(draft.Generals.Where(general => general.SiteId is { } id && !sites.ContainsKey(id))
                .Select(general => general.SiteId!.Value))
            .Distinct().Order().ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException("기본 지도에서 배치된 거점이 사라졌습니다: "
                + string.Join(", ", missing));
        var existing = draft.Cities.ToDictionary(city => city.SiteId);
        var cities = sites.Values.Select(site => existing.TryGetValue(site.Id, out var city)
            ? city : new ScenarioCityEntry(site.Id, 0, 0, 0, 0, 70, [], []))
            .OrderBy(city => city.SiteId).ToArray();
        return draft with { MapId = root.Id, MapRevision = root.Revision, Cities = cities };
    }
}
