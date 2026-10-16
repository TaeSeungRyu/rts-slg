namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

public sealed class GeneralStorySelectorTests
{
    private static GameState State() => new(70, 190, [], [],
        [new General(new(1), "관우", new Dictionary<TroopClass, AptitudeGrade>(), 90, 60, 40)]);

    [Fact]
    public void 같은_사건은_한번만_저장되고_선택은_결정론적이다()
    {
        var candidate = new GeneralStoryCandidate("capture:1", "capture", 1, new(1), new(1));
        var first = GeneralStorySelector.Queue(State(), [candidate], new(1));
        var second = GeneralStorySelector.Queue(first, [candidate], new(1));
        Assert.Single(second.StoryPending);
        Assert.Equal(first.StoryPending[0].Variant, second.StoryPending[0].Variant);
        var loaded = SaveService.Deserialize(SaveService.Serialize(second));
        Assert.Equal(first.StoryPending[0], loaded.StoryPending[0]);
        var resolved = GeneralStorySelector.Resolve(loaded, first.StoryPending[0].Id, "skip");
        Assert.Empty(resolved.StoryPending);
        Assert.Equal("skip", Assert.Single(resolved.StoryHistory).Choice);
        Assert.Same(resolved, GeneralStorySelector.Resolve(resolved, first.StoryPending[0].Id, "again"));
    }

    [Fact]
    public void 적_내부_사건과_없는_장수는_보이지_않는다()
    {
        var result = GeneralStorySelector.Queue(State(),
            [new("enemy", "capture", 1, new(2), new(1)), new("unknown", "growth", 2, new(1), new(9))], new(1));
        Assert.Empty(result.StoryPending);
    }

    [Fact]
    public void 최근_동종_사건은_억제하지만_다른_사건은_우선순위를_따른다()
    {
        var previous = State() with { StoryEventHistory =
            [new("old", "capture", 20, 1, new(1), new(1), null, null, 0)] };
        var result = GeneralStorySelector.Queue(previous,
            [new("capture:2", "capture", 1, new(1), new(1)),
             new("growth:1", "growth", 4, new(1), new(1))], new(1));
        Assert.Equal("growth", Assert.Single(result.StoryPending).Kind);
    }
}
