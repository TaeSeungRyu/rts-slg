namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed class HeroUnlockService
{
    public GameState MarkFactionEliminated(GameState state, FactionId eliminatedFaction)
        => Evaluate(state);

    public GameState Evaluate(GameState state)
    {
        if (state.HeroUnlocks.Count == 0) return state;

        var oldStates = state.HeroStates.ToDictionary(item => item.General);
        var nextStates = new List<HeroUnlockState>(state.HeroUnlocks.Count);
        var changed = oldStates.Count != state.HeroUnlocks.Count;

        foreach (var hero in state.HeroUnlocks.OrderBy(item => item.General.Value))
        {
            var current = oldStates.GetValueOrDefault(hero.General)
                ?? new HeroUnlockState(hero.General, HeroUnlockStatus.Locked);
            HeroUnlockState next;
            if (state.PostingOf(hero.General) is { } posting)
            {
                next = current with { Status = HeroUnlockStatus.Recruited, EligibleFaction = posting.Faction };
            }
            else if (current.Status == HeroUnlockStatus.Excluded || !state.Generals.Any(g => g.Id == hero.General)
                || hero.RequiredGeneral is { } required && !state.Generals.Any(g => g.Id == required))
            {
                next = current with { Status = HeroUnlockStatus.Excluded, EligibleFaction = null };
            }
            else if (state.Dismissed.Any(d => d.General == hero.General))
            {
                next = current with { Status = HeroUnlockStatus.Locked, EligibleFaction = null };
            }
            else
            {
                var eligible = state.Factions.Select(f => f.Id)
                    .OrderBy(id => id.Value)
                    .FirstOrDefault(faction => IsSatisfied(state, hero, faction));
                next = eligible.Value == 0
                    ? current with { Status = HeroUnlockStatus.Locked, EligibleFaction = null }
                    : current with { Status = HeroUnlockStatus.Unlocked, EligibleFaction = eligible };
            }

            if (next != current)
            {
                changed = true;
                next = next with { UpdatedDay = state.Day };
            }
            nextStates.Add(next);
        }

        return changed ? state with { HeroUnlockStates = nextStates } : state;
    }

    public bool IsSatisfied(GameState state, HeroUnlockDefinition hero, FactionId faction)
    {
        if (state.CityCount(faction) <= 0 || state.PostingOf(hero.General) is not null
            || state.Dismissed.Any(d => d.General == hero.General)
            || !state.Generals.Any(g => g.Id == hero.General)) return false;

        if (hero.RequiredGeneral is not { } required) return true;
        if (!state.Generals.Any(g => g.Id == required)) return false;
        return state.PostingOf(required)?.Faction == faction;
    }

    public bool IsWandererSatisfied(GameState state, HeroUnlockDefinition hero, FactionId faction)
        => IsSatisfied(state, hero, faction);
}
