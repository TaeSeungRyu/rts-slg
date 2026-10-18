namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class CampaignScoreboard
{
    public static GameState Apply(GameState state, IReadOnlyList<WorldEvent> events,
        IReadOnlyList<AdvanceTurn> turns, IReadOnlyList<CaptureReport> captures, int woundedPercent)
    {
        var rows = state.Scoreboard.ToDictionary(row => row.Faction);
        foreach (var faction in state.Factions.OrderBy(item => item.Id.Value))
        {
            var id = faction.Id;
            var row = rows.GetValueOrDefault(id) ?? new FactionCampaignStats(id);
            var produced = events.Where(item => item.Faction == id
                    && item.Kind is WorldEventKind.Recruit or WorldEventKind.Conscript)
                .Sum(item => (long)Math.Max(0, item.Amount));
            var research = events.Count(item => item.Faction == id && item.Kind == WorldEventKind.Research);
            var stratagems = events.Count(item => item.Faction == id && item.Kind == WorldEventKind.StratagemSuccess);
            var captured = captures.Count(item => item.NewOwner == id);
            long fatalities = 0;
            foreach (var turn in turns)
            {
                if (turn.Combat is null) continue;
                foreach (var (unitId, damage) in turn.Combat.DamageTaken)
                {
                    var unit = turn.Units.FirstOrDefault(item => item.Id == unitId);
                    if (unit?.Field.Owner != id) continue;
                    fatalities += Math.Max(0, damage) * (100 - Math.Clamp(woundedPercent, 0, 100)) / 100;
                }
            }
            rows[id] = row with
            {
                CapturedCities = row.CapturedCities + captured,
                ProducedTroops = row.ProducedTroops + produced,
                BattleFatalitiesEstimated = row.BattleFatalitiesEstimated + fatalities,
                CompletedResearch = row.CompletedResearch + research,
                SuccessfulStratagems = row.SuccessfulStratagems + stratagems,
            };
        }
        return state with { CampaignStats = rows.Values.OrderBy(row => row.Faction.Value).ToList() };
    }
}
