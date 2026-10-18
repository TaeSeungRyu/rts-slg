namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

public sealed class CampaignScoreboardTests
{
    [Fact]
    public void 주간_생산_연구_점령을_누적하고_저장후에도_유지한다()
    {
        var faction = new FactionId(1);
        var state = new GameState(0, 190,
            [new Faction(faction, "위", new GeneralId(1), 0, "#aabbcc")], [], []);
        var events = new[]
        {
            new WorldEvent(WorldEventKind.Recruit, faction, Amount: 500),
            new WorldEvent(WorldEventKind.Conscript, faction, Amount: 100),
            new WorldEvent(WorldEventKind.Research, faction),
        };
        var captures = new[] { new CaptureReport(new CityId(2), faction, new FactionId(2), [], [], false) };
        var result = CampaignScoreboard.Apply(state, events, [], captures, 30);
        var row = Assert.Single(result.Scoreboard);
        Assert.Equal(600, row.ProducedTroops);
        Assert.Equal(1, row.CompletedResearch);
        Assert.Equal(1, row.CapturedCities);
        var restored = SaveService.Deserialize(SaveService.Serialize(result));
        Assert.Equal(row, Assert.Single(restored.Scoreboard));
        Assert.Equal(600, Assert.Single(CampaignScoreboard.Apply(restored, [], [], [], 30).Scoreboard).ProducedTroops);
    }
}
