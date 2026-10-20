using System.Linq;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private void RunAiTestFixtureQa()
    {
        var baselineMap = BuildTestMap();
        var mapReady = _aiTestFixture is not null && _map.Count > baselineMap.Count
            && _map.Tiles().Count(tile => _map.TerrainAt(tile) == TerrainType.WaterShallow) >= 100;
        var threeFactions = _state.Factions.Count == 3
            && _state.Factions.Select(f => f.Id).Distinct().Count() == 3
            && _state.Factions.All(f => _state.Cities.Count(c => c.Owner == f.Id) >= 3
                && _state.Cities.Any(c => c.Owner == f.Id && c.IsPort)
                && _state.Garrisons.Any(g => _state.Cities.Any(c => c.Id == g.City && c.Owner == f.Id))
                && _state.Assignments.Count(p => p.Faction == f.Id) >= 6);
        var generalsValid = _state.Assignments.All(p =>
            _state.Generals.Any(g => g.Id == p.General)
            && _state.Cities.Any(c => c.Id == p.Location && c.Owner == p.Faction));
        var aiActs = false;
        if (mapReady && threeFactions && generalsValid)
        {
            var planned = _state;
            foreach (var faction in planned.Factions.Where(f => f.Id != Player))
                planned = _ai.PlanWeek(planned, faction.Id);
            aiActs = planned.Armies.Count > _state.Armies.Count
                || planned.Commands.Count > _state.Commands.Count;
        }
        var passed = mapReady && threeFactions && generalsValid && aiActs;
        GD.Print($"[aitestfixtureqa] passed={passed} map={_map.Count}/{baselineMap.Count} "
            + $"factions={_state.Factions.Count} cities={_state.Cities.Count} "
            + $"ports={_state.Cities.Count(c => c.IsPort)} generalsValid={generalsValid} "
            + $"aiActs={aiActs}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
