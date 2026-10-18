namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed class AncientRelicService(IReadOnlyList<AncientRelicDefinition> definitions)
{
    public CommandResult ConvertLegacy(GameState state, FactionId faction, string relicCode)
    {
        var relic = definitions.FirstOrDefault(x => x.Code == relicCode);
        if (relic is null) return CommandResult.Fail("해당 유물을 찾을 수 없습니다.", state);
        var available = state.Discoveries.Select((discovery, index) => (discovery, index))
            .Where(x => x.discovery.Faction == faction && x.discovery.Code == "ancient_relic_clue"
                && x.discovery.ClueCode.Length == 0 && x.discovery.RelicTreasureId.Length == 0)
            .OrderBy(x => x.discovery.Day).ThenBy(x => x.index).Take(2).ToList();
        if (available.Count < 2) return CommandResult.Fail("미분류 단서 2개가 필요합니다.", state);
        var discoveries = state.Discoveries.ToList();
        var id = $"legacy-relic:{faction.Value}:{relic.ClueCode}:{available[1].index}";
        foreach (var entry in available)
            discoveries[entry.index] = entry.discovery with
            { ClueCode = relic.ClueCode, RelicTreasureId = id };
        var treasures = state.Treasures.Append(new FactionTreasure(id, relic.Code, relic.Name, faction,
            state.Day, "ancient_relic_clue")).ToList();
        return CommandResult.Success(state with { ExplorationDiscoveries = discoveries, FactionTreasures = treasures });
    }

    public GameState Resolve(GameState state)
    {
        var discoveries = state.Discoveries.ToList();
        var treasures = state.Treasures.ToList();
        foreach (var relic in definitions)
        {
            foreach (var faction in discoveries.Where(d => d.ClueCode == relic.ClueCode)
                         .Select(d => d.Faction).Distinct().OrderBy(f => f.Value))
            {
                var available = discoveries.Select((discovery, index) => (discovery, index))
                    .Where(x => x.discovery.Faction == faction
                        && x.discovery.Code == "ancient_relic_clue"
                        && x.discovery.ClueCode == relic.ClueCode
                        && x.discovery.RelicTreasureId.Length == 0)
                    .OrderBy(x => x.discovery.Day).ThenBy(x => x.index).ToList();
                for (var i = 0; i + 1 < available.Count; i += 2)
                {
                    var first = available[i];
                    var second = available[i + 1];
                    var id = $"relic:{faction.Value}:{relic.ClueCode}:{second.index}";
                    discoveries[first.index] = first.discovery with { RelicTreasureId = id };
                    discoveries[second.index] = second.discovery with { RelicTreasureId = id };
                    if (!treasures.Any(item => item.Id == id))
                        treasures.Add(new FactionTreasure(id, relic.Code, relic.Name, faction,
                            second.discovery.Day, "ancient_relic_clue"));
                }
            }
        }
        return state with { ExplorationDiscoveries = discoveries, FactionTreasures = treasures };
    }
}
