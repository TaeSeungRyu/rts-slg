namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class EnemyAttackAlertPolicy
{
    public static IReadOnlyList<GeneralStoryEvent> Detect(GameState beforeAi, GameState afterAi,
        FactionId player)
    {
        var previous = beforeAi.Armies.Select(unit => unit.Id).ToHashSet();
        var stories = new List<GeneralStoryEvent>();
        var seen = new HashSet<(CityId Origin, CityId Target)>();
        foreach (var unit in afterAi.Armies.OrderBy(unit => unit.Id.Value))
        {
            if (previous.Contains(unit.Id) || !afterAi.AreHostile(player, unit.Field.Owner)
                || unit.IsSupply || unit.IsTransport || unit.OriginCity is not { } originId
                || unit.Field.Target is not { } destination
                || unit.Field.Mode is not (UnitMode.Attack or UnitMode.March)
                || !afterAi.Cities.Any(city => city.Id == originId && city.Owner == unit.Field.Owner))
                continue;

            var target = afterAi.Cities.FirstOrDefault(city => city.Owner == player
                && city.Position == destination);
            if (target is null || !seen.Add((originId, target.Id))) continue;
            var speaker = Speaker(afterAi, target, player);
            if (speaker is null) continue;
            var origin = afterAi.Cities.First(city => city.Id == originId);
            var source = beforeAi.IsScouted(player, originId) ? origin.Name : "소문";
            stories.Add(new GeneralStoryEvent(
                $"incoming-attack:{beforeAi.Day}:{originId.Value}:{target.Id.Value}",
                "incoming_attack", afterAi.Day, 1, player, speaker.Value, null,
                target.Id, 0, Code: source));
        }
        return stories;
    }

    private static GeneralId? Speaker(GameState state, City target, FactionId player)
    {
        bool PostedHere(GeneralId id) => state.Generals.Any(general => general.Id == id)
            && state.PostingOf(id) is { } posting
            && posting.Faction == player && posting.Location == target.Id;
        if (target.Strategist is { } strategist && PostedHere(strategist)) return strategist;
        if (target.Governor is { } governor && PostedHere(governor)) return governor;
        var local = state.GeneralsAt(target.Id).Where(PostedHere)
            .OrderBy(id => id.Value).ToArray();
        if (local.Length > 0) return local[0];
        var others = state.Assignments.Where(posting => posting.Faction == player
                && posting.Location is { } cityId
                && state.Cities.Any(city => city.Id == cityId && city.Owner == player)
                && state.Generals.Any(general => general.Id == posting.General))
            .Select(posting => posting.General).Distinct().OrderBy(id => id.Value).ToArray();
        return others.Length == 0 ? null
            : others[new SeededRandomSource(unchecked(state.Day * 397 ^ target.Id.Value)).Next(0, others.Length)];
    }
}
