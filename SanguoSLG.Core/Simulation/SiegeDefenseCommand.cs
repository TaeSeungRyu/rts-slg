namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// Phase 11C 수성 지휘관 선정 규칙. 성에 실제 주둔한 태수를 우선하고,
/// 태수가 부재하면 같은 성 주둔 장수 중 수성 적성·무력·지력·ID 순으로 대리를 고른다.
/// </summary>
public static class SiegeDefenseCommand
{
    public static SiegeDefenseLeader Select(GameState state, City city)
    {
        var generalById = state.Generals.ToDictionary(g => g.Id);

        if (city.Governor is { } governorId
            && state.PostingOf(governorId) is { } governorPosting
            && governorPosting.Location == city.Id
            && governorPosting.Faction == city.Owner
            && generalById.TryGetValue(governorId, out var governor))
        {
            return FromGeneral(city.Id, governor, Acting: false);
        }

        var acting = state.Assignments
            .Where(p => p.Location == city.Id && p.Faction == city.Owner)
            .Select(p => generalById.TryGetValue(p.General, out var general) ? general : null)
            .Where(g => g is not null)
            .Cast<General>()
            .OrderByDescending(g => g.AptitudeFor(TroopClass.Defense))
            .ThenByDescending(g => g.Might)
            .ThenByDescending(g => g.Intellect)
            .ThenBy(g => g.Id.Value)
            .FirstOrDefault();

        return acting is null
            ? new SiegeDefenseLeader(city.Id, null, Acting: false, AptitudeGrade.F, 0, 0, "")
            : FromGeneral(city.Id, acting, Acting: true);
    }

    private static SiegeDefenseLeader FromGeneral(CityId city, General general, bool Acting)
        => new(city, general.Id, Acting, general.AptitudeFor(TroopClass.Defense), general.Might, general.Intellect, general.Name);
}

public sealed record SiegeDefenseLeader(
    CityId City,
    GeneralId? General,
    bool Acting,
    AptitudeGrade Aptitude,
    int Might,
    int Intellect,
    string Name);
