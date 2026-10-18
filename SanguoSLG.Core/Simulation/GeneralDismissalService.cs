namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed class GeneralDismissalService
{
    private readonly IRandomSource _random;

    public GeneralDismissalService(IRandomSource random) => _random = random;

    public GameState Dismiss(GameState state, CityId cityId, GeneralId generalId)
    {
        var city = state.Cities.FirstOrDefault(c => c.Id == cityId)
            ?? throw new InvalidOperationException("해고할 도시를 찾을 수 없습니다.");
        var posting = state.PostingOf(generalId);
        if (posting is null || posting.Location != cityId || posting.Faction != city.Owner)
            throw new InvalidOperationException("이 도시에 소속된 장수가 아닙니다.");
        if (state.Factions.Any(f => f.Id == city.Owner && f.Ruler == generalId))
            throw new InvalidOperationException("군주는 해고할 수 없습니다.");
        if (state.IsGeneralBusy(generalId))
            throw new InvalidOperationException("수행 중인 업무가 끝나야 해고할 수 있습니다.");
        if (state.Dismissed.Any(d => d.General == generalId))
            throw new InvalidOperationException("이미 떠난 장수입니다.");

        var cleared = state.ReleaseOfficerDuties(generalId);
        return cleared with
        {
            Cities = cleared.Cities.Select(c => c with
            {
                Governor = c.Governor == generalId ? null : c.Governor,
                Strategist = c.Strategist == generalId ? null : c.Strategist,
            }).ToList(),
            Postings = cleared.Assignments.Where(p => p.General != generalId).ToList(),
            DismissedGenerals = cleared.Dismissed.Append(
                new DismissedGeneral(generalId, city.Owner, state.Day + _random.Next(14, 36))).ToList(),
        };
    }

    public GameState Advance(GameState state)
    {
        if (state.Dismissed.Count == 0) return state;
        var postings = state.Assignments.ToList();
        var remaining = new List<DismissedGeneral>();
        foreach (var dismissed in state.Dismissed.OrderBy(d => d.General.Value))
        {
            if (dismissed.ReturnDay > state.Day) { remaining.Add(dismissed); continue; }
            var destinations = state.Cities.Where(c => c.Owner != dismissed.FormerFaction
                && state.Factions.Any(f => f.Id == c.Owner && f.Id.Value != 0))
                .OrderBy(c => c.Id.Value).ToList();
            if (destinations.Count == 0) { remaining.Add(dismissed); continue; }
            var city = destinations[_random.Next(0, destinations.Count)];
            postings.Add(new GeneralPosting(dismissed.General, city.Owner, city.Id));
        }
        var byGeneral = postings.ToDictionary(p => p.General);
        return state with
        {
            Postings = postings,
            DismissedGenerals = remaining,
            HeroUnlockStates = state.HeroStates.Select(hero => byGeneral.TryGetValue(hero.General, out var posting)
                ? hero with { Status = HeroUnlockStatus.Recruited, EligibleFaction = posting.Faction, UpdatedDay = state.Day }
                : hero).ToList(),
        };
    }
}
