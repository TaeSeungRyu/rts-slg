namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class HeroUnlockServiceTests
{
    private static readonly FactionId Cao = new(1);
    private static readonly FactionId Liu = new(2);
    private static readonly GeneralId CaoCao = new(1);
    private static readonly GeneralId LiuBei = new(2);
    private static readonly GeneralId ZhugeLiang = new(6);

    [Fact]
    public void 선행_장수의_세력으로만_영입_조건이_열린다()
    {
        var hero = new HeroUnlockDefinition(ZhugeLiang, HeroUnlockType.Faction, Cao,
            UnlockYear: 207, RequiredGeneral: LiuBei);
        var state = State(hero, [Posting(LiuBei, Liu)]);
        var service = new HeroUnlockService();

        var evaluated = service.Evaluate(state);

        Assert.Equal(HeroUnlockStatus.Unlocked, Assert.Single(evaluated.HeroStates).Status);
        Assert.True(service.IsSatisfied(evaluated, hero, Liu));
        Assert.False(service.IsSatisfied(evaluated, hero, Cao));
    }

    [Fact]
    public void 선행_장수를_영입하면_새_후보가_나타나고_이탈하면_잠긴다()
    {
        var hero = new HeroUnlockDefinition(ZhugeLiang, HeroUnlockType.Faction, RequiredGeneral: CaoCao);
        var service = new HeroUnlockService();
        var state = State(hero, [Posting(CaoCao, Cao)]);

        Assert.True(service.IsSatisfied(state, hero, Cao));
        var moved = state with { Postings = [Posting(CaoCao, Liu)] };
        Assert.True(service.IsSatisfied(moved, hero, Liu));
        Assert.False(service.IsSatisfied(moved, hero, Cao));
        var departed = service.Evaluate(state with { Postings = [] });
        Assert.Equal(HeroUnlockStatus.Locked, Assert.Single(departed.HeroStates).Status);
    }

    [Fact]
    public void 조건이_없는_장수는_모든_존속_세력이_영입_가능하다()
    {
        var hero = new HeroUnlockDefinition(ZhugeLiang, HeroUnlockType.Region, HomeRegions: ["never"]);
        var state = State(hero, []);
        var service = new HeroUnlockService();

        Assert.True(service.IsSatisfied(state, hero, Cao));
        Assert.True(service.IsSatisfied(state, hero, Liu));
    }

    [Fact]
    public void 시나리오에_선행_장수가_없으면_영구_제외한다()
    {
        var hero = new HeroUnlockDefinition(ZhugeLiang, HeroUnlockType.Faction,
            RequiredGeneral: new GeneralId(100));
        var state = State(hero, []);
        var next = new HeroUnlockService().Evaluate(state);

        Assert.Equal(HeroUnlockStatus.Excluded, Assert.Single(next.HeroStates).Status);
    }

    [Fact]
    public void 이미_배속된_후보는_중복_영입하지_않는다()
    {
        var hero = new HeroUnlockDefinition(ZhugeLiang, HeroUnlockType.Faction);
        var state = State(hero, [Posting(ZhugeLiang, Liu)]);
        var service = new HeroUnlockService();

        Assert.Equal(HeroUnlockStatus.Recruited, Assert.Single(service.Evaluate(state).HeroStates).Status);
        Assert.False(service.IsSatisfied(state, hero, Cao));
    }

    private static GameState State(HeroUnlockDefinition hero, IReadOnlyList<GeneralPosting> postings)
        => new(
            1,
            190,
            [new Faction(Cao, "위", CaoCao, 1000, "#2d5fd0"),
                new Faction(Liu, "촉", LiuBei, 1000, "#2c8c46")],
            [City(1, Cao), City(2, Liu)],
            [General(1), General(2), General(6)],
            Postings: postings,
            HeroUnlockDefinitions: [hero],
            HeroUnlockStates: [new HeroUnlockState(hero.General, HeroUnlockStatus.Locked)]);

    private static GeneralPosting Posting(GeneralId general, FactionId faction)
        => new(general, faction, new CityId(faction.Value));

    private static General General(int id)
        => new(new GeneralId(id), $"장수{id}", new Dictionary<TroopClass, AptitudeGrade>(), 60, 60, 60);

    private static City City(int id, FactionId owner)
        => new(new CityId(id), $"도시{id}", new HexCoord(id, 0), owner, 1000, CastleSize.Small);
}
