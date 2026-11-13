namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public sealed class GeneralStoryCollectorTests
{
    [Fact]
    public void 관문_점령은_주민_민심_안정_대화를_생성하지_않는다()
    {
        var speaker = new General(new(1), "선봉", new Dictionary<TroopClass, AptitudeGrade>(), 80, 60, 50);
        var gate = new City(new(7), "호뢰관", new HexCoord(3, 0), new(1), 100, Gate: GateSize.Small);
        var field = new FieldUnit(new(9), new(1), gate.Position, 2, 2, 1,
            MovementDomain.Land, UnitMode.Attack, null, 0);
        var occupier = new CombatUnit(field, new CombatStats(1000, 10, 10), new TroopPool(1000, 0),
            UnitCombatState.Create(60), 80, VanguardId: speaker.Id);
        var state = new GameState(7, 190, [new Faction(new(1), "아군", speaker.Id, 0, "red")],
            [gate], [speaker], FieldArmies: [occupier]);
        var report = new CaptureReport(gate.Id, new(1), new(2), [], [], false, [occupier.Id]);

        Assert.Empty(GeneralStoryCollector.Collect(state, state, [], [report], []));
    }

    [Fact]
    public void 동맹_대화는_상대_군주와_소재도시_참모를_선택한다()
    {
        var envoy = new General(new(1), "사절", new Dictionary<TroopClass, AptitudeGrade>(), 40, 50, 90);
        var ruler = new General(new(2), "군주", new Dictionary<TroopClass, AptitudeGrade>(), 50, 70, 70);
        var advisor = new General(new(3), "참모", new Dictionary<TroopClass, AptitudeGrade>(), 40, 95, 80);
        var origin = new City(new(1), "출발", new HexCoord(0, 0), new(1), 100);
        var near = new City(new(2), "가까운 성", new HexCoord(1, 0), new(2), 100);
        var court = new City(new(3), "군주 성", new HexCoord(6, 0), new(2), 100,
            Strategist: advisor.Id);
        var state = new GameState(7, 190,
            [new Faction(new(1), "아군", envoy.Id, 0, "red"), new Faction(new(2), "상대", ruler.Id, 0, "blue")],
            [origin, near, court], [envoy, ruler, advisor],
            Postings: [new(envoy.Id, new(1), origin.Id), new(ruler.Id, new(2), court.Id),
                new(advisor.Id, new(2), court.Id)]);

        var audience = DiplomacyAudience.Select(state, new(2), origin.Position);
        Assert.Equal(court.Id, audience.City?.Id);
        Assert.Equal(ruler.Id, audience.Ruler);
        Assert.Equal(advisor.Id, audience.Advisor);

        var success = Assert.Single(GeneralStoryCollector.Collect(state, state, [], [],
            [new(WorldEventKind.AllianceSuccess, new(1), envoy.Id, origin.Id, Code: "2")]));
        Assert.Equal("alliance_success_advisor", success.Kind);
        Assert.Equal(ruler.Id, success.SecondSpeaker);
        Assert.Equal(advisor.Id.Value, success.Amount);
        Assert.Equal(court.Id, success.City);

        var withoutAdvisor = state with { Postings = state.Assignments.Where(posting => posting.General != advisor.Id).ToArray() };
        var failure = Assert.Single(GeneralStoryCollector.Collect(withoutAdvisor, withoutAdvisor, [], [],
            [new(WorldEventKind.AllianceFail, new(1), envoy.Id, origin.Id, Code: "2")]));
        Assert.Equal("alliance_fail", failure.Kind);
        Assert.Equal(0, failure.Amount);
    }

    [Fact]
    public void 정찰_계략과_동맹은_서로_다른_대화로_분류한다()
    {
        var state = new GameState(7, 190, [], [],
            [new General(new(1), "관우", new Dictionary<TroopClass, AptitudeGrade>(), 90, 60, 40)]);
        var events = new WorldEvent[]
        {
            new(WorldEventKind.StratagemSuccess, new(1), new GeneralId(1), Code: "scout"),
            new(WorldEventKind.StratagemFail, new(1), new GeneralId(1), Code: "arson"),
            new(WorldEventKind.AllianceSuccess, new(1), new GeneralId(1)),
        };

        Assert.Equal(new[] { "scout_success", "stratagem_fail", "alliance_success" },
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

    [Fact]
    public void 생산_보상은_금과_군량을_대화에_함께_보존한다()
    {
        var general = new General(new(1), "장수", new Dictionary<TroopClass, AptitudeGrade>(), 50, 50, 50);
        var state = new GameState(7, 190, [], [], [general]);
        var candidates = GeneralStoryCollector.Collect(state, state, [], [],
            [new WorldEvent(WorldEventKind.ProductionComplete, new(1), general.Id, Amount: 70,
                Code: "village", ExtraAmount: 230)]);
        var story = Assert.Single(GeneralStorySelector.Queue(state, candidates, new(1)).StoryPending);
        Assert.Equal(70, story.Amount);
        Assert.Equal(230, story.ExtraAmount);
    }
}
