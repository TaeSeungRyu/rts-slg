namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>전투/점령/세계 사건의 확정 결과에서만 이야기 후보를 만든다.</summary>
public static class GeneralStoryCollector
{
    public static IReadOnlyList<GeneralStoryCandidate> Collect(GameState before, GameState after,
        IReadOnlyList<AdvanceTurn> turns, IReadOnlyList<CaptureReport> captures,
        IReadOnlyList<WorldEvent> worldEvents, IReadOnlyList<SiegeExchange>? sieges = null)
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

        foreach (var siege in (sieges ?? []).Where(s => s.WallDamage > 0).GroupBy(s => s.City)
            .Select(g => g.First()).OrderBy(s => s.City.Value))
        {
            var city = after.Cities.FirstOrDefault(c => c.Id == siege.City);
            var oldOwner = before.Cities.FirstOrDefault(c => c.Id == siege.City)?.Owner;
            if (city is null || oldOwner != city.Owner) continue;
            var speaker = city.Governor ?? city.Strategist
                ?? after.Assignments.Where(p => p.Location == city.Id && p.Faction == city.Owner)
                    .OrderBy(p => p.General.Value).Select(p => (GeneralId?)p.General).FirstOrDefault();
            if (speaker is null) continue;
            candidates.Add(new($"wall:{city.Id.Value}", "wall", 3,
                city.Owner, speaker.Value, City: city.Id, Amount: siege.WallDamage));
        }
        foreach (var unit in after.Armies.Where(u => u.Field.Owner != EventHuntService.EventFaction
            && u.Pool.Active > 0 && u.Provisions >= 0 && u.VanguardId is not null)
            .OrderBy(u => u.Id.Value))
        {
            var daily = System.Math.Max(1, (long)unit.Pool.Active * 10
                * unit.ProvisionsUpkeepPercent / 1_000_000L);
            if (unit.Provisions / daily > 3) continue;
            candidates.Add(new($"low_food:{unit.Id.Value}", "low_food", 3,
                unit.Field.Owner, unit.VanguardId!.Value, unit.AdjutantId,
                Code: unit.Id.Value.ToString()));
        }
        foreach (var city in after.Cities.OrderBy(c => c.Id.Value))
        {
            var old = before.Cities.FirstOrDefault(c => c.Id == city.Id && c.Owner == city.Owner);
            if (old is null || city.Security >= old.Security || city.RecruitmentOfficer is not { } recruiter) continue;
            candidates.Add(new($"security:{city.Id.Value}", "security", 5, city.Owner,
                city.SecurityOfficer ?? recruiter,
                city.SecurityOfficer is { } officer && officer != recruiter ? recruiter : null,
                city.Id, old.Security - city.Security));
        }
        foreach (var discovery in after.Discoveries.Skip(before.Discoveries.Count)
            .Where(d => d.Kind == ExplorationResultKind.DivineBeast))
            candidates.Add(new($"clue:{discovery.City.Value}:{discovery.Explorer.Value}:{discovery.Day}",
                "beast_appeared", 2, discovery.Faction, discovery.Explorer, City: discovery.City,
                Code: discovery.Code));

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
