namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed class AncientRelicService(IReadOnlyList<AncientRelicDefinition> definitions)
{
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
