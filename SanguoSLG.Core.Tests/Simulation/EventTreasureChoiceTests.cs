namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class EventTreasureChoiceTests
{
    private static readonly EventHuntService Service = new(new HexMap(-2, 2, -2, 2), [],
        new EventTreasureLoader().LoadFromDirectory(TestData.DataDirectory()));

    [Theory]
    [InlineData("great_tiger", "tiger_cub")]
    [InlineData("dragon", "dragon_egg")]
    [InlineData("phoenix", "phoenix_egg")]
    public void 신수별_3종_중_1개만_선택하고_저장_재개한다(string source, string chosen)
    {
        Assert.Equal(3, Service.RewardChoices(source).Count);
        var state = new GameState(30, 190, [], [], [],
            EventEncounters: [new("hunt", source, new(1), new(1), 20, DefeatedDay: 30,
                RewardFaction: new(1), RewardChoicePending: true)],
            FactionTreasures: [new("hunt:reward", Service.RewardChoices(source)[0].Code,
                Service.RewardChoices(source)[0].Name, new(1), 30, "hunt")]);
        var result = Service.ChooseReward(state, "hunt", new(1), chosen);
        Assert.True(result.Ok, result.Error);
        var resumed = SaveService.Deserialize(SaveService.Serialize(result.State));
        Assert.Equal(chosen, Assert.Single(resumed.Treasures).Code);
        Assert.False(Service.ChooseReward(resumed, "hunt", new(1), chosen).Ok);
    }

    [Fact]
    public void AI도_선택을_확정하고_플레이어_대기_선택은_건드리지_않는다()
    {
        var encounter = new EventEncounter("ai", "dragon", new(2), new(2), 20,
            DefeatedDay: 30, RewardFaction: new(2), RewardChoicePending: true);
        var player = new EventEncounter("player", "phoenix", new(1), new(1), 20,
            DefeatedDay: 30, RewardFaction: new(1), RewardChoicePending: true);
        var state = new GameState(30, 190, [], [], [], EventEncounters: [encounter, player],
            FactionTreasures: [new("ai:reward", "dragon_scale", "용린 갑옷", new(2), 30, "ai"),
                new("player:reward", "phoenix_feather", "봉황 깃", new(1), 30, "player")]);
        var chosen = Service.ChooseAiRewards(state, new(1));
        Assert.False(chosen.Encounters[0].RewardChoicePending);
        Assert.True(chosen.Encounters[1].RewardChoicePending);
        Assert.Equal("dragon_scale", chosen.Treasures[0].Code);
    }
}
