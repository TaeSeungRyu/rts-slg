namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed class GenericRecruitmentTests
{
    private static (GameState State, City City, GeneralId Actor) Setup(int gold = 5000)
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var postings = scenario.Postings ?? [];
        var city = scenario.Cities.First(c => postings.Any(p => p.Location == c.Id && p.Faction == c.Owner));
        var actor = postings.First(p => p.Location == city.Id && p.Faction == city.Owner).General;
        var state = GameState.FromScenario(scenario) with
        {
            Cities = scenario.Cities.Select(c => c.Id == city.Id ? c with { Gold = gold } : c).ToArray(),
        };
        return (state, city, actor);
    }

    [Fact]
    public void 무관과_문관은_같은_도시의_월간_시행권을_공유하고_저장된다()
    {
        var (state, city, actor) = Setup();
        var first = GenericRecruitmentService.Recruit(state, city with { Gold = 5000 }, actor, true);
        Assert.True(first.Ok, first.Error);
        Assert.Equal(4000, first.State.Cities.Single(c => c.Id == city.Id).Gold);
        var general = first.State.Generals.Single(g => g.Id == first.State.GenericRecruitmentHistory.Single().General);
        Assert.InRange(general.Might, 50, 75);
        Assert.InRange(general.Intellect, 20, 45);
        Assert.InRange(general.Politics, 20, 45);
        Assert.All(general.Aptitudes.Values, grade => Assert.InRange(grade, AptitudeGrade.D, AptitudeGrade.B));
        Assert.Single(general.Passives);
        Assert.Equal(general.Might >= 70, general.BattleActive is not null);
        Assert.Equal(city.Id, first.State.PostingOf(general.Id)?.Location);
        var again = GenericRecruitmentService.Recruit(first.State,
            first.State.Cities.Single(c => c.Id == city.Id), actor, false);
        Assert.False(again.Ok);
        Assert.Equal(first.State.Cities.Single(c => c.Id == city.Id).Gold,
            again.State.Cities.Single(c => c.Id == city.Id).Gold);
        var saved = SaveService.Deserialize(SaveService.Serialize(first.State));
        Assert.Equal(general.Name, saved.Generals.Single(g => g.Id == general.Id).Name);
        Assert.Single(saved.GenericRecruitmentHistory);
        var nextMonth = saved with { Day = GameState.DaysPerMonth + 1 };
        Assert.True(GenericRecruitmentService.Recruit(nextMonth,
            nextMonth.Cities.Single(c => c.Id == city.Id), actor, false).Ok);
    }

    [Fact]
    public void 문관_능력_스킬과_금_부족_조건을_지킨다()
    {
        var (state, city, actor) = Setup(999);
        Assert.False(GenericRecruitmentService.Recruit(state, city with { Gold = 999 }, actor, false).Ok);
        Assert.Empty(state.GenericRecruitmentHistory);
        var result = GenericRecruitmentService.Recruit(state with
        {
            Cities = state.Cities.Select(c => c.Id == city.Id ? c with { Gold = 1000 } : c).ToArray(),
        }, city with { Gold = 1000 }, actor, false);
        Assert.True(result.Ok, result.Error);
        var general = result.State.Generals.Single(g => g.Id == result.State.GenericRecruitmentHistory.Single().General);
        Assert.InRange(general.Might, 20, 35);
        Assert.InRange(general.Intellect, 50, 75);
        Assert.InRange(general.Politics, 50, 75);
        Assert.All(general.Aptitudes.Values, grade => Assert.InRange(grade, AptitudeGrade.F, AptitudeGrade.C));
        Assert.Equal(general.Intellect >= 70 || general.Politics >= 70, general.AdminPassives?.Count == 1);
        Assert.Null(general.BattleActive);
    }

    [Fact]
    public void 대화_페이지와_임명_확인은_저장후에도_중복_지급하지_않는다()
    {
        var (state, city, actor) = Setup();
        var recruited = GenericRecruitmentService.Recruit(state, city with { Gold = 5000 }, actor, true).State;
        var id = recruited.GenericRecruitmentHistory.Single().General;
        recruited = GenericRecruitmentService.SetDialoguePage(recruited, id, 3);
        var loaded = SaveService.Deserialize(SaveService.Serialize(recruited));
        Assert.Equal(3, loaded.GenericRecruitmentHistory.Single().DialoguePage);
        loaded = GenericRecruitmentService.MarkPresented(loaded, id);
        Assert.True(loaded.GenericRecruitmentHistory.Single().Presented);
        Assert.Single(loaded.Generals, g => g.Id == id);
        Assert.Single(loaded.Assignments, p => p.General == id);
    }

    [Fact]
    public void 같은_조건의_모집은_같은_장수를_만들고_기존_이름은_피한다()
    {
        var (state, city, actor) = Setup();
        var first = GenericRecruitmentService.Recruit(state, city with { Gold = 5000 }, actor, false);
        var second = GenericRecruitmentService.Recruit(state, city with { Gold = 5000 }, actor, false);
        var firstGeneral = first.State.Generals.Last();
        var secondGeneral = second.State.Generals.Last();
        Assert.Equal((firstGeneral.Name, firstGeneral.Might, firstGeneral.Intellect, firstGeneral.Politics,
            firstGeneral.PortraitPath), (secondGeneral.Name, secondGeneral.Might, secondGeneral.Intellect,
            secondGeneral.Politics, secondGeneral.PortraitPath));
        Assert.Equal(1, first.State.Generals.Count(g => g.Name == firstGeneral.Name));
    }
}
