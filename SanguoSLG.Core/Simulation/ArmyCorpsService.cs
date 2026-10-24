namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class ArmyCorpsService
{
    public static GameState Save(GameState state, int? id, FactionId faction,
        GeneralId commander, IEnumerable<CityId> assignedCities, ArmyCorpsPolicy policy)
    {
        var cities = assignedCities.Distinct().OrderBy(city => city.Value).ToList();
        if (cities.Count == 0) throw new InvalidOperationException("군단에 성 또는 항구를 하나 이상 배치하세요.");
        if (cities.Any(city => !state.Cities.Any(entry => entry.Id == city && entry.Owner == faction)))
            throw new InvalidOperationException("자신의 성과 항구만 배치할 수 있습니다.");
        if (state.Corps.Any(corps => corps.Id != id && corps.Faction == faction
            && corps.Cities.Any(cities.Contains)))
            throw new InvalidOperationException("이미 다른 군단에 배치된 거점이 있습니다.");
        var posting = state.PostingOf(commander);
        if (posting is null || posting.Faction != faction || posting.Location is not { } location
            || !cities.Contains(location) || state.IsGeneralInField(commander))
            throw new InvalidOperationException("군단장은 맡길 거점에 주둔 중인 장수여야 합니다.");
        if (state.Corps.Any(corps => corps.Id != id && corps.Commander == commander))
            throw new InvalidOperationException("이미 다른 군단을 지휘 중인 장수입니다.");
        var existing = state.Corps.FirstOrDefault(corps => corps.Id == id && corps.Faction == faction);
        if (id is not null && existing is null)
            throw new InvalidOperationException("수정할 군단을 찾지 못했습니다.");
        var nextId = existing?.Id ?? (state.Corps.Count == 0 ? 1 : state.Corps.Max(corps => corps.Id) + 1);
        var saved = new ArmyCorps(nextId, faction, commander, cities, policy,
            existing?.AiNextWarDecisionDay ?? 0);
        return state with { ArmyCorpsList = state.Corps.Where(corps => corps.Id != nextId)
            .Append(saved).OrderBy(corps => corps.Id).ToList() };
    }

    public static GameState Disband(GameState state, int id, FactionId faction)
        => state with { ArmyCorpsList = state.Corps.Where(corps => corps.Id != id
            || corps.Faction != faction).ToList() };
}
