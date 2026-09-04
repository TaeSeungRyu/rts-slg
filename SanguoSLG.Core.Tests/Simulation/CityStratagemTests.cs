namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>도시 계략 5종 — 거리 비례 소요일·지력 성공률·정찰 전제·효과 정산.</summary>
public class CityStratagemTests
{
    private static readonly CommandBalance B = new();
    private static readonly BalanceConfig Bal = new(MonthlyTaxPerCity: 0);

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private sealed class FixedRandom(params int[] values) : IRandomSource
    {
        private readonly int[] _v = values;
        private int _i;
        public int Next(int minInclusive, int maxExclusive) => _v[_i++ % _v.Length];
    }

    private static CommandService Service() => new(B, Troops, Bal);

    private static General Gen(int id, int intellect = 80, int loyalty = 100) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 50, Intellect: intellect, Politics: 50, Loyalty: loyalty);

    private static City Mine(int gold = 2000) =>
        new(new CityId(1), "아군성", new HexCoord(0, 0), new FactionId(1), 3000, CastleSize.Medium, Gold: gold);

    private static City Enemy(int id = 2, int q = 6, int? governor = null) =>
        new(new CityId(id), "적성", new HexCoord(q, 0), new FactionId(2), 3000, CastleSize.Medium,
            Gold: 1000, Security: 80, Wall: 1200,
            Governor: governor is { } g ? new GeneralId(g) : null);

    private static GameState State(IEnumerable<City> cities, IEnumerable<General> generals,
        IEnumerable<GeneralPosting>? postings = null, IEnumerable<CityIntel>? intel = null) =>
        new(1, 1, new List<Faction>(), cities.ToList(), generals.ToList(),
            Postings: postings?.ToList(), ScoutedCities: intel?.ToList());

    private static CityIntel Scouted() => new(new FactionId(1), new CityId(2));

    private static CommandRequest Req(string kind) =>
        new(new CityId(1), CommandKind.CityStratagem, new GeneralId(1), Facility: kind, TargetCity: new CityId(2));

    // ── 규칙 계산 ──

    [Fact]
    public void 소요일_거리에_비례하고_항상_7일을_넘는다()
    {
        Assert.Equal(9, CityStratagems.Days(new HexCoord(0, 0), new HexCoord(3, 0), B));   // 7 + 1×2
        Assert.Equal(11, CityStratagems.Days(new HexCoord(0, 0), new HexCoord(6, 0), B));  // 7 + 2×2
        Assert.Equal(13, CityStratagems.Days(new HexCoord(0, 0), new HexCoord(9, 0), B));
        Assert.Equal(17, CityStratagems.Days(new HexCoord(0, 0), new HexCoord(15, 0), B));
    }

    [Fact]
    public void 성공률_지력차_보정에_상하한이_있다()
    {
        Assert.Equal(50, CityStratagems.SuccessPercent(80, 80));
        Assert.Equal(90, CityStratagems.SuccessPercent(100, 40));  // clamp 상한
        Assert.Equal(10, CityStratagems.SuccessPercent(40, 100));  // clamp 하한
        Assert.Equal(90, CityStratagems.SuccessPercent(100, null)); // 태수 없음 = 40
    }

    // ── 발행 검증 ──

    [Fact]
    public void 발행_정찰_없이는_다른_계략을_걸수없다()
    {
        var s = State([Mine(), Enemy()], [Gen(1)]);
        var r = Service().Issue(s, Req("arson"));
        Assert.False(r.Ok);
        Assert.Contains("정찰", r.Error);

        Assert.True(Service().Issue(s, Req("scout")).Ok); // 정찰 자체는 전제 없음
    }

    [Fact]
    public void 발행_소요일이_거리비례로_잡힌다()
    {
        var s = State([Mine(), Enemy(q: 6)], [Gen(1)], intel: [Scouted()]);
        var r = Service().Issue(s, Req("arson"));
        Assert.True(r.Ok, r.Error);
        var cmd = r.State.Commands.Single();
        Assert.Equal(11, cmd.CompletionDay - cmd.StartDay); // 거리 6 → 7+4
        Assert.Equal(new CityId(2), cmd.TargetCity);
    }

    // ── 정산(성공/실패) ──

    private static GameState Advance(GameState issued, int days, int roll)
        => new WorldEngine(Bal, B, random: new FixedRandom(roll)).AdvanceDays(issued, days);

    [Fact]
    public void 정찰_성공하면_정보가_등록되고_후속_계략이_열린다()
    {
        var s = State([Mine(), Enemy()], [Gen(1)]);
        var issued = Service().Issue(s, Req("scout"));
        var done = Advance(issued.State, 11, roll: 0); // 성공(0 < 90: 태수 없음)

        Assert.True(done.IsScouted(new FactionId(1), new CityId(2)));
        Assert.True(Service().Issue(done, Req("arson")).Ok); // 이제 방화 가능
    }

    [Fact]
    public void 실패하면_아무_효과가_없다()
    {
        var s = State([Mine(), Enemy(governor: 9)], [Gen(1, intellect: 80), Gen(9, intellect: 80)], intel: [Scouted()]);
        var issued = Service().Issue(s, Req("arson"));
        var done = Advance(issued.State, 11, roll: 99); // 실패(99 ≥ 50)

        Assert.Equal(3000, done.Cities.First(c => c.Id == new CityId(2)).Provisions); // 무효
    }

    [Fact]
    public void 방화_성공하면_군량이_탄다()
    {
        var s = State([Mine(), Enemy()], [Gen(1)], intel: [Scouted()]);
        var issued = Service().Issue(s, Req("arson"));
        var done = Advance(issued.State, 11, roll: 0);

        Assert.Equal(2400, done.Cities.First(c => c.Id == new CityId(2)).Provisions); // −20%
    }

    [Fact]
    public void 절취_성공하면_금이_수행_도시로_넘어온다()
    {
        var s = State([Mine(gold: 2000), Enemy()], [Gen(1)], intel: [Scouted()]);
        var issued = Service().Issue(s, Req("steal"));
        var done = Advance(issued.State, 11, roll: 0);

        Assert.Equal(800, done.Cities.First(c => c.Id == new CityId(2)).Gold);   // 1000 − 20%
        Assert.Equal(2200, done.Cities.First(c => c.Id == new CityId(1)).Gold);  // 예치
    }

    [Fact]
    public void 성벽파괴와_선동이_성벽과_치안을_깎는다()
    {
        var s = State([Mine(), Enemy()], [Gen(1), Gen(2)], intel: [Scouted()]);
        var issued = Service().Issue(s, Req("wall_break"));
        issued = Service().Issue(issued.State,
            new CommandRequest(new CityId(1), CommandKind.CityStratagem, new GeneralId(2), Facility: "incite", TargetCity: new CityId(2)));
        var done = Advance(issued.State, 11, roll: 0);

        var enemy = done.Cities.First(c => c.Id == new CityId(2));
        Assert.Equal(1200 - 120, enemy.Wall);   // 최대(레벨0 중성 1200)의 10%
        Assert.Equal(70, enemy.Security);        // −10
    }

    [Fact]
    public void 발행_이간은_도시계략에서_제거되어_거부된다()
    {
        var s = State([Mine(), Enemy()],
            [Gen(1), Gen(10, loyalty: 150), Gen(11, loyalty: 90)],
            postings:
            [
                new GeneralPosting(new GeneralId(1), new FactionId(1), new CityId(1)), // 수행 장수 주둔
                new GeneralPosting(new GeneralId(10), new FactionId(2), new CityId(2)),
                new GeneralPosting(new GeneralId(11), new FactionId(2), new CityId(2)),
            ],
            intel: [Scouted()]);

        var issued = Service().Issue(s, Req("sow_discord"));
        Assert.False(issued.Ok);
        Assert.Contains("계략", issued.Error);
    }
}
