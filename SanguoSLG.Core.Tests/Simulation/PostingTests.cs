namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>장수 배속(소유·배속 기반) — 소속·주둔 쿼리와 명령 소속 검증.</summary>
public class PostingTests
{
    private static General G(int id, int politics = 90) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 60, Intellect: 60, Politics: politics);

    private static City Town(int id, int owner) =>
        new(new CityId(id), $"c{id}", new HexCoord(0, 0), new FactionId(owner), 5000,
            CastleSize.Medium, Gold: 1000, Population: 100_000, Ore: 5000);

    // 위(1): 도시 1. 촉(2): 도시 2. 장수 10=위@1, 11=촉@2, 12=재야
    private static GameState World()
    {
        var cities = new List<City> { Town(1, 1), Town(2, 2) };
        var generals = new List<General> { G(10), G(11), G(12) };
        var postings = new List<GeneralPosting>
        {
            new(new GeneralId(10), new FactionId(1), new CityId(1)),
            new(new GeneralId(11), new FactionId(2), new CityId(2)),
        };
        return new GameState(1, 1, new List<Faction>(), cities, generals, Postings: postings);
    }

    [Fact]
    public void 배속_도시별_세력별_조회()
    {
        var s = World();
        Assert.Equal(new[] { new GeneralId(10) }, s.GeneralsAt(new CityId(1)));
        Assert.Equal(new[] { new GeneralId(11) }, s.GeneralsOf(new FactionId(2)));
        Assert.Null(s.PostingOf(new GeneralId(12))); // 재야
    }

    [Fact]
    public void 명령_소속_세력_장수만_그_도시에서_수행한다()
    {
        var svc = new CommandService(new CommandBalance());
        var s = World();

        // 위 장수(10)가 위 도시(1)에서 세율 → 성공
        Assert.True(svc.Issue(s, new CommandRequest(new CityId(1), CommandKind.SetTaxRate, new GeneralId(10), Value: 30)).Ok);
    }

    [Fact]
    public void 명령_다른_세력_도시에서는_거부된다()
    {
        var svc = new CommandService(new CommandBalance());
        var s = World();

        // 위 장수(10)가 촉 도시(2)에서 명령 → 소속 불일치
        var r = svc.Issue(s, new CommandRequest(new CityId(2), CommandKind.SetTaxRate, new GeneralId(10), Value: 30));
        Assert.False(r.Ok);
        Assert.Contains("소유 세력", r.Error);
    }

    [Fact]
    public void 명령_주둔하지_않는_도시에서는_거부된다()
    {
        // 위 장수(10)를 세력은 위지만 위치를 다른 도시로: 도시 1에 있고 도시 3에서 명령하려 함
        var cities = new List<City> { Town(1, 1), Town(3, 1) };
        var s = new GameState(1, 1, new List<Faction>(), cities, new List<General> { G(10) },
            Postings: new List<GeneralPosting> { new(new GeneralId(10), new FactionId(1), new CityId(1)) });

        var svc = new CommandService(new CommandBalance());
        var r = svc.Issue(s, new CommandRequest(new CityId(3), CommandKind.SetTaxRate, new GeneralId(10), Value: 30));
        Assert.False(r.Ok);
        Assert.Contains("주둔", r.Error);
    }

    [Fact]
    public void 명령_재야_장수는_거부된다()
    {
        var svc = new CommandService(new CommandBalance());
        var s = World();
        var r = svc.Issue(s, new CommandRequest(new CityId(1), CommandKind.SetTaxRate, new GeneralId(12), Value: 30));
        Assert.False(r.Ok);
        Assert.Contains("재야", r.Error);
    }

    [Fact]
    public void 데이터_postings_json은_세력_소유_도시에_배속된다()
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var state = GameState.FromScenario(scenario);
        var cityOwner = scenario.Cities.ToDictionary(c => c.Id, c => c.Owner);

        Assert.NotEmpty(state.Assignments);
        foreach (var p in state.Assignments)
        {
            Assert.True(scenario.Generals.Any(g => g.Id == p.General), "배속 장수가 명단에 있어야 한다");
            if (p.Location is { } loc)
            {
                Assert.True(cityOwner.TryGetValue(loc, out var owner) && owner == p.Faction,
                    $"장수 {p.General.Value}: 주둔 도시 소유가 소속 세력과 달라");
            }
        }
    }
}
