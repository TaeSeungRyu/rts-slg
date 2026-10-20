namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>11b 성벽 강화 — 도시별 5단계(20~100%), 강화는 최대 내구도만 증가한다.</summary>
public class WallResearchTests
{
    private static readonly CommandBalance B = new();
    private static readonly BalanceConfig Bal = new(MonthlyTaxPerCity: 0);

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private static CommandService Service() => new(B, Troops);

    private static General Wit(int id, int intellect = 60) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 50, Intellect: intellect, Politics: 50);

    private static City Town(int id, CastleSize size, bool workshop, int wall, int gold = 5000) =>
        new(new CityId(id), $"c{id}", new HexCoord(id, 0), new FactionId(1), 3000, size,
            Gold: gold, Wall: wall, Workshop: workshop);

    [Fact]
    public void 성벽최대_단계별_비율이_적용된다()
    {
        // 소성 완료값 3000: 미연구 20%(600) → 4단계 100%(3000).
        Assert.Equal(600, CastleWall.Max(CastleSize.Small, Bal, 0));
        Assert.Equal(1200, CastleWall.Max(CastleSize.Small, Bal, 1));
        Assert.Equal(3000, CastleWall.Max(CastleSize.Small, Bal, 4));
        Assert.Equal(1200, CastleWall.Max(CastleSize.Medium, Bal, 0)); // 중성 6000의 20%
    }

    [Fact]
    public void 발행_성벽연구는_공방이_없어도_가능하지만_금은_필요하다()
    {
        var noWorkshop = Town(1, CastleSize.Medium, workshop: false, wall: 1200);
        var r1 = Service().Issue(WorldState(noWorkshop, Wit(1)), Req());
        Assert.True(r1.Ok, r1.Error);

        var poor = Town(1, CastleSize.Medium, workshop: true, wall: 1200, gold: 500);
        var r2 = Service().Issue(WorldState(poor, Wit(1)), Req()); // 1단계 비용 1000 > 500
        Assert.False(r2.Ok);
        Assert.Contains("금", r2.Error);
    }

    [Fact]
    public void 성벽강화는_연구비분담을무시하고_대상성의금만소비한다()
    {
        var target = Town(1, CastleSize.Medium, workshop: false, wall: 1200, gold: 1500);
        var other = Town(2, CastleSize.Small, workshop: false, wall: 600, gold: 9000);
        var state = new GameState(1, 1, new List<Faction>(), [target, other], [Wit(1)]);
        var request = Req() with
        {
            ResearchFunding = [new ResearchFundingShare(other.Id, 100)],
        };

        var result = Service().Issue(state, request);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(500, result.State.Cities.Single(city => city.Id == target.Id).Gold);
        Assert.Equal(9000, result.State.Cities.Single(city => city.Id == other.Id).Gold);
    }

    [Fact]
    public void 대상성의금이부족하면_다른성의금이많아도_명령과장수잠금없이실패한다()
    {
        var target = Town(1, CastleSize.Medium, workshop: false, wall: 1200, gold: 999);
        var other = Town(2, CastleSize.Small, workshop: false, wall: 600, gold: 9000);
        var general = Wit(1);
        var state = new GameState(1, 1, new List<Faction>(), [target, other], [general],
            Postings: [new GeneralPosting(general.Id, target.Owner, target.Id)]);
        var request = Req() with
        {
            ResearchFunding = [new ResearchFundingShare(other.Id, 100)],
        };

        var result = Service().Issue(state, request);

        Assert.False(result.Ok);
        Assert.Same(state, result.State);
        Assert.Empty(result.State.Commands);
        Assert.Equal(target.Id, result.State.PostingOf(general.Id)!.Location);
    }

    [Fact]
    public void 루프_성벽연구_완료시_해당_도시_최대치만_증가하고_내구도는_회복하지않는다()
    {
        var world = new WorldEngine(Bal, B);
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City>
            {
                Town(1, CastleSize.Medium, workshop: true, wall: 1200), // 미연구 20%
                Town(2, CastleSize.Small, workshop: false, wall: 600),  // 같은 세력 다른 성
            },
            new List<General> { Wit(1) });

        var issued = Service().Issue(s, Req());
        Assert.True(issued.Ok, issued.Error);
        Assert.Equal(5000 - 1000, issued.State.Cities.First(c => c.Id == new CityId(1)).Gold); // 1단계 1000

        var done = world.AdvanceDays(issued.State, 30);

        var c1 = done.Cities.First(c => c.Id == new CityId(1));
        var c2 = done.Cities.First(c => c.Id == new CityId(2));
        Assert.Equal(1, c1.WallLevel);
        Assert.Equal(0, c2.WallLevel);
        // 1단계 최대치 2400이지만 현재 내구도 1200은 그대로다.
        Assert.Equal(2400, CastleWall.Max(c1.Castle, Bal, c1.WallLevel));
        Assert.Equal(1200, c1.Wall);
        Assert.Equal(600, c2.Wall);
    }

    [Fact]
    public void 손상된_성벽도_강화로_회복되지않는다()
    {
        var city = Town(1, CastleSize.Medium, workshop: true, wall: 500);
        var issued = Service().Issue(WorldState(city, Wit(1)), Req());
        var done = new WorldEngine(Bal, B).AdvanceDays(issued.State, 30);

        Assert.Equal(1, done.Cities.Single().WallLevel);
        Assert.Equal(500, done.Cities.Single().Wall);
    }

    [Fact]
    public void 성벽강화와_전투교리는_서로다른_장수로_병행한다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, CastleSize.Medium, workshop: true, wall: 1200) },
            new List<General> { Wit(1), Wit(2) });

        var first = Service().Issue(s, Req());
        Assert.True(first.Ok);
        var second = Service().Issue(first.State,
            new CommandRequest(new CityId(1), CommandKind.Research, new GeneralId(2), TroopCode: "swordsman"));
        Assert.True(second.Ok, second.Error);
        Assert.Equal(2, second.State.Commands.Count);
    }

    [Fact]
    public void 성벽강화는_같은도시에서만_중복을_막는다()
    {
        var firstCity = Town(1, CastleSize.Medium, workshop: true, wall: 1200);
        var secondCity = Town(2, CastleSize.Small, workshop: false, wall: 600);
        var state = new GameState(1, 1, [], [firstCity, secondCity],
            [Wit(1), Wit(2), Wit(3)]);
        var first = Service().Issue(state, Req());
        Assert.True(first.Ok, first.Error);

        var duplicate = Service().Issue(first.State, Req() with { Main = new GeneralId(2) });
        Assert.False(duplicate.Ok);
        var otherCity = Service().Issue(first.State,
            Req() with { City = secondCity.Id, Main = new GeneralId(3) });
        Assert.True(otherCity.Ok, otherCity.Error);
        Assert.Equal(2, otherCity.State.Commands.Count);
    }

    private static CommandRequest Req()
        => new(new CityId(1), CommandKind.Research, new GeneralId(1), TroopCode: FactionResearch.WallCode);

    private static GameState WorldState(City city, General general)
        => new(1, 1, new List<Faction>(), new List<City> { city }, new List<General> { general });
}
