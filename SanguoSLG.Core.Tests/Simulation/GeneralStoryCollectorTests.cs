namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

public sealed class GeneralStoryCollectorTests
{
    [Fact]
    public void 정찰_계략과_동맹은_서로_다른_대화로_분류한다()
    {
        var state = new GameState(7, 190, [], [],
            [new General(new(1), "관우", new Dictionary<TroopClass, AptitudeGrade>(), 90, 60, 40)]);
        var events = new WorldEvent[]
        {
            new(WorldEventKind.StratagemSuccess, new(1), new GeneralId(1)),
            new(WorldEventKind.StratagemFail, new(1), new GeneralId(1)),
            new(WorldEventKind.AllianceSuccess, new(1), new GeneralId(1)),
        };

        Assert.Equal(new[] { "stratagem", "stratagem", "alliance_success" },
            GeneralStoryCollector.Collect(state, state, [], [], events).Select(candidate => candidate.Kind));
    }

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
            [new(WorldEventKind.Research, new(1), new GeneralId(1), Amount: 20)]);
        var queued = GeneralStorySelector.Queue(state, candidates, new(1));
        Assert.Equal(state.Generals, queued.Generals);
        Assert.Equal(20, Assert.Single(queued.StoryPending).Amount);
    }

    [Fact]
    public void 성장_결과는_대화_이벤트를_생성하지_않는다()
    {
        var state = new GameState(7, 190, [], [],
            [new General(new(1), "관우", new Dictionary<TroopClass, AptitudeGrade>(), 90, 60, 40)]);
        var events = new WorldEvent[]
        {
            new(WorldEventKind.GeneralGrowth, new(1), new GeneralId(1)),
            new(WorldEventKind.AdministrationGrowth, new(1), new GeneralId(1)),
            new(WorldEventKind.AptitudeGrowth, new(1), new GeneralId(1)),
        };

        Assert.Empty(GeneralStoryCollector.Collect(state, state, [], [], events));
    }
}
