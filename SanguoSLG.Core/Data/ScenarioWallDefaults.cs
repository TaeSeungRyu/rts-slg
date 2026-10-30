namespace SanguoSLG.Core.Data;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public static class ScenarioWallDefaults
{
    public static int ForSite(MapEditorSite site, BalanceConfig balance)
    {
        if (site.Kind == "gate") return site.Size == "medium" ? 3000 : 2000;
        var castle = site.Kind == "port" ? CastleSize.Small : site.Size switch
        {
            "large" => CastleSize.Large,
            "medium" => CastleSize.Medium,
            _ => CastleSize.Small,
        };
        return CastleWall.Max(castle, balance, wallLevel: 0);
    }

    public static ScenarioDraft Normalize(ScenarioDraft draft, ScenarioMapPreset map, BalanceConfig balance)
    {
        var sites = map.Sites.Where(site => site.Kind is "city" or "port" or "gate")
            .ToDictionary(site => site.Id);
        return draft with { Cities = draft.Cities.Select(city => sites.TryGetValue(city.SiteId, out var site)
            ? city with { Wall = ForSite(site, balance) } : city).ToArray() };
    }
}
