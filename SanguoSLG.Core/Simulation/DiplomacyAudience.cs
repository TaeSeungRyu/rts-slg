namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed record DiplomacyAudience(City? City, GeneralId? Ruler, GeneralId? Advisor)
{
    public static DiplomacyAudience Select(GameState state, FactionId targetFaction, HexCoord origin)
    {
        var faction = state.Factions.FirstOrDefault(value => value.Id == targetFaction);
        var ruler = faction is not null && state.Generals.Any(general => general.Id == faction.Ruler)
            ? faction.Ruler : (GeneralId?)null;
        var rulerPosting = ruler is { } rulerId ? state.PostingOf(rulerId) : null;
        var city = state.Cities.FirstOrDefault(value => value.Owner == targetFaction
            && value.Id == rulerPosting?.Location)
            ?? state.Cities.Where(value => value.Owner == targetFaction)
                .OrderBy(value => value.Position.Distance(origin)).ThenBy(value => value.Id.Value)
                .FirstOrDefault();
        if (city is null) return new(null, ruler, null);

        var residents = state.GeneralsAt(city.Id)
            .Where(id => id != ruler && state.PostingOf(id)?.Faction == targetFaction)
            .Select(id => state.Generals.FirstOrDefault(general => general.Id == id))
            .Where(general => general is not null).Select(general => general!)
            .OrderByDescending(general => AdministrationGrowth.EffectiveIntellectRounded(general))
            .ThenBy(general => general.Id.Value).ToList();
        var advisor = city.Strategist is { } strategist && residents.Any(general => general.Id == strategist)
            ? strategist : residents.FirstOrDefault()?.Id;
        return new(city, ruler, advisor);
    }
}
