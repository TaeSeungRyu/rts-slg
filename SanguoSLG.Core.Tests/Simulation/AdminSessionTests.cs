namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>내정 전용 세션(게임 씬 seam) TDD — 도시 조회·내정 명령·주 단위 진행. 전투 없음.</summary>
public class AdminSessionTests
{
    private static readonly CommandBalance B = new();

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private static General Gov(int id, int politics = 90) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 50, Intellect: 50, Politics: politics);

    private static City Town(int id, int owner, int ore = 5000, int population = 100_000, int tax = 20) =>
        new(new CityId(id), $"c{id}", new HexCoord(id, 0), new FactionId(owner), 3000, CastleSize.Medium,
            Gold: 2000, Population: population, Ore: ore, TaxRate: tax);

    private static AdminSession Session(GameState state, int player = 1) =>
        new(state, new FactionId(player), new CommandService(B, Troops),
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100), B));

    // 플레이어 세력 1 도시 2개 + 적 세력 2 도시 1개, 각 도시에 주둔 태수.
    private static GameState World() => new(1, 1,
        new List<Faction>(),
        new List<City> { Town(1, 1), Town(2, 1), Town(3, 2) },
        new List<General> { Gov(1), Gov(2), Gov(3) },
        Postings: new List<GeneralPosting>
        {
            new(new GeneralId(1), new FactionId(1), new CityId(1)),
            new(new GeneralId(2), new FactionId(1), new CityId(2)),
            new(new GeneralId(3), new FactionId(2), new CityId(3)),
        });

    [Fact]
    public void 조회_플레이어_도시만_보인다()
    {
        var s = Session(World());
        Assert.Equal(new[] { new CityId(1), new CityId(2) }, s.PlayerCities().Select(c => c.Id));
    }

    [Fact]
    public void 조회_수행가능_장수는_주둔_비잠금만()
    {
        var s = Session(World());
        Assert.Equal(new[] { new GeneralId(1) }, s.AvailableGenerals(new CityId(1)));

        // 모병으로 장수 잠기면 목록에서 빠진다.
        Assert.True(s.Issue(new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman")).Ok);
        Assert.Empty(s.AvailableGenerals(new CityId(1)));
    }

    [Fact]
    public void 발행_적_도시_명령은_거부된다()
    {
        var s = Session(World());
        var r = s.Issue(new CommandRequest(new CityId(3), CommandKind.Recruit, new GeneralId(3), TroopCode: "swordsman"));
        Assert.False(r.Ok);
        Assert.Contains("내 도시", r.Error);
    }

    [Fact]
    public void 진행_모병_명령이_7일뒤_병력으로_정산되고_장수가_풀린다()
    {
        var s = Session(World());
        Assert.True(s.Issue(new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman")).Ok);
        Assert.NotEmpty(s.PendingAt(new CityId(1)));

        s.AdvanceWeek();

        Assert.Empty(s.PendingAt(new CityId(1)));
        Assert.Empty(s.State.Commands);
        var g = s.State.Garrisons.Single(x => x.City == new CityId(1) && x.TroopCode == "swordsman");
        Assert.True(g.Troops > 0, "모병 병력이 대기 병력으로 정산된다");
        Assert.Contains(new GeneralId(1), s.AvailableGenerals(new CityId(1))); // 잠금 해제
    }

    [Fact]
    public void 진행_세율_명령이_적용된다()
    {
        var s = Session(World());
        Assert.True(s.Issue(new CommandRequest(new CityId(1), CommandKind.SetTaxRate, new GeneralId(1), Value: 40)).Ok);
        s.AdvanceWeek();
        Assert.Equal(40, s.State.Cities.Single(c => c.Id == new CityId(1)).TaxRate);
    }

    [Fact]
    public void 진행_한주는_7일이다()
    {
        var s = Session(World());
        var before = s.State.Day;
        s.AdvanceWeek();
        Assert.Equal(before + 7, s.State.Day);
    }
}
