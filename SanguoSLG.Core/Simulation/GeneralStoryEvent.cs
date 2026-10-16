namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>확정된 게임 결과에서 만든 대화 후보. 원본 결과와 무관한 적 정보는 담지 않는다.</summary>
public sealed record GeneralStoryCandidate(
    string TriggerId, string Kind, int Priority, FactionId Faction,
    GeneralId Speaker, GeneralId? SecondSpeaker = null, CityId? City = null,
    int Amount = 0, string Code = "");

/// <summary>저장되는 대화 인스턴스. 선택 결과도 같은 레코드에 보존해 재적용을 막는다.</summary>
public sealed record GeneralStoryEvent(
    string Id, string Kind, int Day, int Priority, FactionId Faction,
    GeneralId Speaker, GeneralId? SecondSpeaker, CityId? City,
    int Variant, int Amount = 0, string Code = "", string? Choice = null,
    bool Presented = false);

/// <summary>Phase 20A 공통 선택기. 순서와 대사는 안정 해시로 결정하며 전역 난수를 소비하지 않는다.</summary>
public static class GeneralStorySelector
{
    public static GameState Queue(GameState state, IEnumerable<GeneralStoryCandidate> candidates,
        FactionId viewer, int variantCount = 3)
    {
        if (variantCount < 1) throw new ArgumentOutOfRangeException(nameof(variantCount));
        var known = state.StoryPending.Select(e => e.Id)
            .Concat(state.StoryHistory.Select(e => e.Id)).ToHashSet(StringComparer.Ordinal);
        var recent = state.StoryPending.Concat(state.StoryHistory).ToArray();
        var pending = state.StoryPending.ToList();
        foreach (var candidate in candidates
            .Where(c => c.Faction == viewer && state.Generals.Any(g => g.Id == c.Speaker)
                && (c.SecondSpeaker is null || state.Generals.Any(g => g.Id == c.SecondSpeaker)))
            .OrderBy(c => c.Priority).ThenBy(c => c.TriggerId, StringComparer.Ordinal))
        {
            var id = $"{state.Day}:{candidate.TriggerId}";
            if (!known.Add(id)) continue;
            var cooldown = candidate.Kind is "low_food" or "security" or "research" or "production" ? 28 : 60;
            if (recent.Any(e => e.Kind == candidate.Kind && e.Speaker == candidate.Speaker
                && e.SecondSpeaker == candidate.SecondSpeaker && state.Day - e.Day < cooldown)) continue;
            var hash = StableHash(id);
            pending.Add(new GeneralStoryEvent(id, candidate.Kind, state.Day, candidate.Priority,
                candidate.Faction, candidate.Speaker, candidate.SecondSpeaker, candidate.City,
                (int)(hash % (uint)variantCount), candidate.Amount, candidate.Code));
        }
        return state with { PendingStoryEvents = pending };
    }

    public static GameState Resolve(GameState state, string id, string? choice)
    {
        var story = state.StoryPending.FirstOrDefault(e => e.Id == id);
        if (story is null) return state;
        return state with
        {
            PendingStoryEvents = state.StoryPending.Where(e => e.Id != id).ToArray(),
            StoryEventHistory = state.StoryHistory.Append(story with { Choice = choice, Presented = true }).ToArray(),
        };
    }

    private static uint StableHash(string value)
    {
        uint hash = 2166136261;
        foreach (var ch in value) hash = (hash ^ ch) * 16777619;
        return hash;
    }
}
