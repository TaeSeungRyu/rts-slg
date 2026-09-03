namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>11b 성벽 강화 — 도시별 5단계(20~100%), 시작 미강화 20%, 완료 시 해당 도시 성벽 증축.</summary>
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
    public void 루프_성벽연구_완료시_해당_도시_성벽만_증축된다()
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
        // 1단계 40%: 중성 6000×40%=2400. 선택하지 않은 소성은 기존 600 유지.
        Assert.Equal(2400, c1.Wall);
        Assert.Equal(600, c2.Wall);
    }

    [Fact]
    public void 발행_성벽연구도_동시1개_연구제한에_걸린다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, CastleSize.Medium, workshop: true, wall: 1200) },
            new List<General> { Wit(1), Wit(2) });

        var first = Service().Issue(s, Req());
        Assert.True(first.Ok);
        // 성벽 연구 진행 중 병종 연구를 다른 장수로 발행 → 거부(공통 슬롯).
        var second = Service().Issue(first.State,
            new CommandRequest(new CityId(1), CommandKind.Research, new GeneralId(2), TroopCode: "swordsman"));
        Assert.False(second.Ok);
        Assert.Contains("하나의 연구", second.Error);
    }

    private static CommandRequest Req()
        => new(new CityId(1), CommandKind.Research, new GeneralId(1), TroopCode: FactionResearch.WallCode);

    private static GameState WorldState(City city, General general)
        => new(1, 1, new List<Faction>(), new List<City> { city }, new List<General> { general });
}
