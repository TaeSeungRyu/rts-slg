namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>전투/점령/세계 사건의 확정 결과에서만 이야기 후보를 만든다.</summary>
public static class GeneralStoryCollector
{
    public static IReadOnlyList<GeneralStoryCandidate> Collect(GameState before, GameState after,
        IReadOnlyList<AdvanceTurn> turns, IReadOnlyList<CaptureReport> captures,
        IReadOnlyList<WorldEvent> worldEvents)
    {
        var candidates = new List<GeneralStoryCandidate>();
        var armies = before.Armies.Concat(after.Armies)
            .Concat(turns.SelectMany(t => t.Units))
            .GroupBy(u => u.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var capture in captures.OrderBy(x => x.City.Value))
        {
            var occupier = capture.OccupyingUnits.Select(id => armies.GetValueOrDefault(id))
                .FirstOrDefault(u => u?.VanguardId is not null);
            if (occupier?.VanguardId is not { } speaker) continue;
            candidates.Add(new($"capture:{capture.City.Value}:{capture.NewOwner.Value}", "capture", 1,
                capture.NewOwner, speaker, occupier.AdjutantId, capture.City));
        }

        for (var turnIndex = 0; turnIndex < turns.Count; turnIndex++)
        {
            var turn = turns[turnIndex];
            if (turn.Combat is not { } combat) continue;
            foreach (var exchange in (combat.Exchanges ?? []).OrderBy(x => x.Target.Value))
            {
                if (!armies.TryGetValue(exchange.Attacker, out var attacker)
                    || !armies.TryGetValue(exchange.Target, out var target)
                    || attacker.VanguardId is not { } speaker
                    || attacker.AdjutantId is not { } adjutant
                    || target.Pool.Active <= 0) continue;
                if (turn.Units.Any(u => u.Id == target.Id && u.Pool.Active > 0)) continue;
                candidates.Add(new($"victory:{turnIndex}:{attacker.Id.Value}:{target.Id.Value}",
                    "victory", 3, attacker.Field.Owner, speaker, adjutant));
            }
        }

        for (var index = 0; index < worldEvents.Count; index++)
        {
            var e = worldEvents[index];
            if (e.General is not { } speaker) continue;
            var (kind, priority) = e.Kind switch
            {
                WorldEventKind.EventUnitDefeated => ("beast_defeated", 2),
                WorldEventKind.EventUnitAppeared => ("beast_appeared", 2),
                WorldEventKind.GeneralGrowth or WorldEventKind.AdministrationGrowth
                    or WorldEventKind.AptitudeGrowth => ("growth", 4),
                WorldEventKind.Research => ("research", 5),
                WorldEventKind.ProductionComplete => ("production", 5),
                WorldEventKind.AllianceSuccess or WorldEventKind.AllianceFail
                    or WorldEventKind.StratagemSuccess or WorldEventKind.StratagemFail => ("diplomacy", 6),
                _ => ("", 0),
            };
            if (priority == 0) continue;
            candidates.Add(new($"world:{index}:{e.Kind}:{speaker.Value}", kind, priority,
                e.Faction, speaker, City: e.City, Amount: e.Amount, Code: e.Code));
        }
        return candidates;
    }
}
