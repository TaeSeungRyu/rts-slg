namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

public sealed class GeneralStoryCollectorTests
{
    [Fact]
    public void 실제_세계사건만_후보로_만들고_높은_우선순위를_먼저_저장한다()
    {
        var state = new GameState(7, 190, [], [],
            [new General(new(1), "관우", new Dictionary<TroopClass, AptitudeGrade>(), 90, 60, 40)]);
        var events = new WorldEvent[]
        {
            new(WorldEventKind.Research, new(1), new GeneralId(1)),
            new(WorldEventKind.EventUnitDefeated, new(1), new GeneralId(1)),
            new(WorldEventKind.Explore, new(1), new GeneralId(1)),
        };
        var candidates = GeneralStoryCollector.Collect(state, state, [], [], events);
        Assert.Equal(2, candidates.Count);
        var queued = GeneralStorySelector.Queue(state, candidates, new(1));
        Assert.Equal(new[] { "beast_defeated", "research" }, queued.StoryPending.Select(e => e.Kind));
    }

    [Fact]
    public void 보상_원본_상태는_수정하지_않는다()
    {
        var state = new GameState(7, 190, [], [],
            [new General(new(1), "관우", new Dictionary<TroopClass, AptitudeGrade>(), 90, 60, 40)]);
        var candidates = GeneralStoryCollector.Collect(state, state, [], [],
            [new(WorldEventKind.GeneralGrowth, new(1), new GeneralId(1), Amount: 20)]);
        var queued = GeneralStorySelector.Queue(state, candidates, new(1));
        Assert.Equal(state.Generals, queued.Generals);
        Assert.Equal(20, Assert.Single(queued.StoryPending).Amount);
    }
}
