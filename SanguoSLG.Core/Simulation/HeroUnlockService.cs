namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>위인 해금 조건을 현재 GameState 기준으로 판정하고 상태를 갱신한다.</summary>
public sealed class HeroUnlockService
{
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
        => hero.ConditionList.All(c => IsSatisfied(state, c, faction));

    public bool IsWandererSatisfied(GameState state, HeroUnlockDefinition hero, FactionId faction)
        => hero.WandererConditionList.Count > 0 && hero.WandererConditionList.All(c => IsSatisfied(state, c, faction));

    private FactionId? EligibleFaction(GameState state, HeroUnlockDefinition hero, HeroUnlockState current)
    {
        if (current.Status == HeroUnlockStatus.Wanderer)
        {
            foreach (var candidate in state.Factions.Select(f => f.Id).Where(f => state.CityCount(f) > 0))
            {
                if (IsWandererSatisfied(state, hero, candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        if (hero.Type == HeroUnlockType.Faction && hero.Faction is { } faction)
        {
            return state.CityCount(faction) > 0 && IsSatisfied(state, hero, faction) ? faction : null;
        }

        if (hero.Type == HeroUnlockType.Region)
        {
            foreach (var candidate in state.Factions.Select(f => f.Id).Where(f => state.CityCount(f) > 0))
            {
                if (IsSatisfied(state, hero, candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        return null;
    }

    private static bool IsSatisfied(GameState state, HeroUnlockCondition condition, FactionId faction)
        => condition.Code switch
        {
            "owned_cities" => state.CityCount(faction) >= condition.Value,
            "owned_region_cities" => state.Cities.Count(c => c.Owner == faction && c.Region == condition.Region) >= condition.Value,
            "city_security_at_least" => state.Cities.Any(c => c.Owner == faction && c.Security >= condition.Value),
            "research_level" => !string.IsNullOrWhiteSpace(condition.TroopCode)
                && state.ResearchOf(faction, condition.TroopCode) >= condition.Value,
            "major_troop" => !string.IsNullOrWhiteSpace(condition.TroopCode)
                && state.IsMajorTroop(faction, condition.TroopCode),
            "gold_at_least" or "monthly_gold_at_least" => state.Factions.FirstOrDefault(f => f.Id == faction)?.Gold >= condition.Value,
            _ => false,
        };
}
