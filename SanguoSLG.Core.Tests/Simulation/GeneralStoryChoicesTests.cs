namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public sealed class GeneralStoryChoicesTests
{
    [Theory]
    [InlineData("growth")]
    [InlineData("diplomacy")]
    public void 연결_화면이_없는_대화는_화면을_연다고_안내하지_않는다(string kind)
    {
        var result = GeneralStoryChoices.Apply(State(kind), "story:1", "a");
        Assert.True(result.Ok);
        Assert.DoesNotContain("엽니다", result.Message);
    }

    private static GameState State(string kind, int gold = 200, int security = 80, int wall = 400)
    {
        var story = new GeneralStoryEvent("story:1", kind, 7, 1, new(1), new(1), new GeneralId(2), new CityId(1), 0);
        return new GameState(7, 190, [],
            [new City(new(1), "장안", new HexCoord(0, 0), new(1), 100,
                Gold: gold, Security: security, Wall: wall, WallLevel: 0)],
            [new General(new(1), "관우", new Dictionary<TroopClass, AptitudeGrade>(), 90, 60, 40),
             new General(new(2), "장비", new Dictionary<TroopClass, AptitudeGrade>(), 90, 60, 40)],
            PendingStoryEvents: [story]);
    }

    [Fact]
    public void 승전_두번째_선택지는_부관에게만_경험치를_준다()
    {
        var result = GeneralStoryChoices.Apply(State("victory"), "story:1", "b");
        Assert.True(result.Ok);
        Assert.Equal(0, result.State.Generals[0].Experience);
        Assert.Equal(20, result.State.Generals[1].Experience);
        Assert.False(GeneralStoryChoices.Apply(result.State, "story:1", "a").Ok);
    }

    [Fact]
    public void 점령_지원은_금과_치안을_한번만_변경하고_저장된다()
    {
        var result = GeneralStoryChoices.Apply(State("capture"), "story:1", "a");
        Assert.True(result.Ok);
        Assert.Equal(100, result.State.Cities[0].Gold);
        Assert.Equal(82, result.State.Cities[0].Security);
        var loaded = SaveService.Deserialize(SaveService.Serialize(result.State));
        Assert.False(GeneralStoryChoices.Apply(loaded, "story:1", "a").Ok);
        Assert.Single(loaded.StoryHistory);
    }

    [Fact]
    public void 자원_부족과_상한은_원본_사건을_남긴다()
    {
        var poor = State("capture", gold: 99);
        Assert.Same(poor, GeneralStoryChoices.Apply(poor, "story:1", "a").State);
        var full = State("wall", wall: 600);
        Assert.False(GeneralStoryChoices.Apply(full, "story:1", "a").Ok);
        Assert.Single(full.StoryPending);
    }

    [Fact]
    public void 생산_선택은_내정_경험치만_더한다()
    {
        var result = GeneralStoryChoices.Apply(State("production"), "story:1", "a");
        Assert.Equal(10, result.State.Generals[0].AdminExperience);
        Assert.Equal(0, result.State.Generals[0].Experience);
    }

    [Fact]
    public void 부상병은_군량을_소비하고_실제_부상자만_회복한다()
    {
        var field = new FieldUnit(new(42), new(1), new HexCoord(1, 1), 1, 1, 1,
            MovementDomain.Land, UnitMode.Standby, null, 42);
        var unit = new CombatUnit(field, new CombatStats(100, 10, 10, 100),
            new TroopPool(90, 10), UnitCombatState.Create(0), MaxTroops: 100,
            Provisions: 80, VanguardId: new GeneralId(1));
        var state = State("wounded") with
        {
            FieldArmies = [unit],
            PendingStoryEvents = [State("wounded").StoryPending[0] with { Code = "42" }],
        };
        var result = GeneralStoryChoices.Apply(state, "story:1", "a");
        Assert.True(result.Ok);
        Assert.Equal(100, result.State.Armies[0].Pool.Active);
        Assert.Equal(0, result.State.Armies[0].Pool.Wounded);
        Assert.Equal(30, result.State.Armies[0].Provisions);
    }

    [Fact]
    public void 성벽과_치안_상한을_넘기지_않는다()
    {
        var wall = GeneralStoryChoices.Apply(State("wall", wall: 580), "story:1", "a");
        Assert.True(wall.Ok);
        Assert.Equal(600, wall.State.Cities[0].Wall);
        var security = GeneralStoryChoices.Apply(State("security", security: 99), "story:1", "a");
        Assert.True(security.Ok);
        Assert.Equal(100, security.State.Cities[0].Security);
    }
}
