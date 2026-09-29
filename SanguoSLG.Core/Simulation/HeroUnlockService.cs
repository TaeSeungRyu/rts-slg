namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>위인 해금 조건을 현재 GameState 기준으로 판정하고 상태를 갱신한다.</summary>
public sealed class HeroUnlockService
{
    public GameState MarkFactionEliminated(GameState state, FactionId eliminatedFaction)
    {
        if (state.HeroUnlocks.Count == 0)
        {
            return state;
        }

        var factionHeroes = state.HeroUnlocks
            .Where(h => h.Type == HeroUnlockType.Faction && h.Faction == eliminatedFaction)
            .Select(h => h.General)
            .ToHashSet();
        if (factionHeroes.Count == 0)
        {
            return state;
        }

        var states = state.HeroStates.ToDictionary(s => s.General);
        var changed = false;
        foreach (var general in factionHeroes)
        {
            var current = states.GetValueOrDefault(general)
                ?? new HeroUnlockState(general, HeroUnlockStatus.Locked);
            if (current.Status == HeroUnlockStatus.Locked)
            {
                states[general] = current with
                {
                    Status = HeroUnlockStatus.Wanderer,
                    EligibleFaction = null,
                    UpdatedDay = state.Day,
                };
                changed = true;
            }
        }

        return changed
            ? state with { HeroUnlockStates = states.Values.OrderBy(s => s.General.Value).ToList() }
            : state;
    }

    public GameState Evaluate(GameState state)
    {
        if (state.HeroUnlocks.Count == 0)
        {
            return state;
        }

        var states = state.HeroStates.ToDictionary(s => s.General);
        var changed = false;

        foreach (var hero in state.HeroUnlocks)
        {
            var current = states.GetValueOrDefault(hero.General)
                ?? new HeroUnlockState(hero.General, HeroUnlockStatus.Locked);

            if (current.Status is HeroUnlockStatus.Recruited or HeroUnlockStatus.Excluded or HeroUnlockStatus.Unlocked)
            {
                states[hero.General] = current;
                continue;
            }

            if (!UnlockYearReached(state, hero))
            {
                states[hero.General] = current;
                continue;
            }

            var eligible = EligibleFaction(state, hero, current);
            if (eligible is null)
            {
                states[hero.General] = current;
                continue;
            }

            states[hero.General] = current with
            {
                Status = HeroUnlockStatus.Unlocked,
                EligibleFaction = eligible,
                UpdatedDay = state.Day,
            };
            changed = true;
        }

        return changed
            ? state with { HeroUnlockStates = states.Values.OrderBy(s => s.General.Value).ToList() }
            : state;
    }

    public bool IsSatisfied(GameState state, HeroUnlockDefinition hero, FactionId faction)
        => CandidateMatches(state, hero, faction);

    public bool IsWandererSatisfied(GameState state, HeroUnlockDefinition hero, FactionId faction)
        => state.CityCount(faction) > 0;

    private FactionId? EligibleFaction(GameState state, HeroUnlockDefinition hero, HeroUnlockState current)
    {
        if (current.Status == HeroUnlockStatus.Wanderer)
        {
            foreach (var candidate in state.Factions.Select(f => f.Id).Where(f => state.CityCount(f) > 0)
                .OrderBy(f => f.Value))
            {
                return candidate;
            }

            return null;
        }

        if (hero.Type == HeroUnlockType.Faction && hero.Faction is { } faction)
        {
            return state.CityCount(faction) > 0 ? faction : null;
        }

        if (hero.Type == HeroUnlockType.Region)
        {
            foreach (var candidate in state.Factions.Select(f => f.Id).Where(f => state.CityCount(f) > 0)
                .OrderBy(f => f.Value))
            {
                if (CandidateMatches(state, hero, candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        return null;
    }

    private static bool UnlockYearReached(GameState state, HeroUnlockDefinition hero)
    {
        var requiredYear = hero.UnlockYear > 0
            ? hero.UnlockYear
            : state.Generals.FirstOrDefault(g => g.Id == hero.General)?.UnlockYear ?? 0;
        return requiredYear <= 0 || state.Year >= requiredYear;
    }

    private static bool CandidateMatches(GameState state, HeroUnlockDefinition hero, FactionId faction)
    {
        if (state.CityCount(faction) <= 0)
        {
            return false;
        }
        if (hero.Type == HeroUnlockType.Faction)
        {
            return hero.Faction == faction;
        }
        if (hero.CityList.Count > 0 && state.Cities.Any(c => c.Owner == faction && hero.CityList.Contains(c.Id)))
        {
            return true;
        }
        if (hero.RegionList.Count > 0 && state.Cities.Any(c => c.Owner == faction && hero.RegionList.Contains(c.Region)))
        {
            return true;
        }
        return hero.CityList.Count == 0 && hero.RegionList.Count == 0;
    }
}
