namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>충성 운영(design-general-lifecycle §1) — 월 급여(충성 낮은 순 우선)·미지급 하락·배신(재야화).</summary>
public class LoyaltyOperationTests
{
    private static readonly CommandBalance B = new();

    // 수입 0(급여만 관찰) — 세율·기본 금·마을 금 전부 0.
    private static readonly BalanceConfig NoIncome = new(
        MonthlyTaxPerCity: 0, GoldBaseSmall: 0, GoldBaseMedium: 0, GoldBaseLarge: 0, VillageGold: 0);

    private static WorldEngine World(int seed, BalanceConfig? bal = null) =>
        new(bal ?? NoIncome, B, random: new SeededRandomSource(seed));

    private static General Gen(int id, int loyalty) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 50, Intellect: 50, Politics: 50, Loyalty: loyalty);

    private static City Town(int id, int gold) =>
        new(new CityId(id), $"c{id}", new HexCoord(0, 0), new FactionId(1), 0, CastleSize.Medium, Gold: gold);

    private static GameState State(int cityGold, IEnumerable<General> members, int rulerLoyalty = 100)
    {
        var gens = new List<General> { Gen(99, rulerLoyalty) };
        gens.AddRange(members);
        var postings = gens.Select(g => new GeneralPosting(g.Id, new FactionId(1), new CityId(1))).ToList();
        return new GameState(1, 1,
            new List<Faction> { new(new FactionId(1), "f1", new GeneralId(99), 0, "#fff") },
            new List<City> { Town(1, cityGold) }, gens, Postings: postings);
    }

    private static int Loy(GameState s, int id) => s.Generals.First(g => g.Id == new GeneralId(id)).Loyalty;

    [Fact]
    public void 급여를_다_지급하면_충성이_유지된다()
    {
        var s = State(cityGold: 1000, new[] { Gen(1, 100), Gen(2, 100) });

        var after = World(1).AdvanceDays(s, GameState.DaysPerMonth);

        Assert.Equal(100, Loy(after, 1));
        Assert.Equal(100, Loy(after, 2));
        Assert.Equal(1000 - 2 * NoIncome.GeneralSalaryPerMonth, after.Cities.Single().Gold); // 2명분 차감
    }

    [Fact]
    public void 급여가_부족하면_충성_낮은순_우선_지급되고_못받은_장수는_하락한다()
    {
        // 급여 20 · 금 20 → 1명만 지급. 충성 낮은 g1(100) 먼저, g2(150)는 미지급 → 하락.
        var s = State(cityGold: 20, new[] { Gen(1, 100), Gen(2, 150) });

        var after = World(1).AdvanceDays(s, GameState.DaysPerMonth);

        Assert.Equal(100, Loy(after, 1));                 // 지급받아 유지
        Assert.InRange(Loy(after, 2), 148, 149);          // 미지급 −1~2
        Assert.Equal(0, after.Cities.Single().Gold);
    }

    [Fact]
    public void 충성_100이상_주둔장수는_배신하지_않는다()
    {
        var s = State(cityGold: 100000, new[] { Gen(1, 100), Gen(2, 200) });

        for (var seed = 0; seed < 15; seed++)
        {
            var after = World(seed).AdvanceDays(s, GameState.DaysPerMonth);
            Assert.NotNull(after.PostingOf(new GeneralId(1)));
            Assert.NotNull(after.PostingOf(new GeneralId(2)));
        }
    }

    [Fact]
    public void 충성이_낮은_주둔장수는_배신해_재야가_될수있다()
    {
        // 급여는 넉넉(배신만 관찰). 충성 50 → 스케일 100%면 50% 배신.
        var s = State(cityGold: 100000, new[] { Gen(1, 50) });

        var defected = 0;
        for (var seed = 0; seed < 20; seed++)
        {
            var after = World(seed).AdvanceDays(s, GameState.DaysPerMonth);
            if (after.PostingOf(new GeneralId(1)) is null) { defected++; }
        }

        Assert.True(defected is > 0 and < 20, $"배신은 확률적 — 일부만(배신 {defected}/20)");
    }

    [Fact]
    public void 군주는_충성이_낮아도_배신하지_않는다()
    {
        var s = State(cityGold: 100000, new[] { Gen(1, 100) }, rulerLoyalty: 10);

        for (var seed = 0; seed < 15; seed++)
        {
            var after = World(seed).AdvanceDays(s, GameState.DaysPerMonth);
            Assert.NotNull(after.PostingOf(new GeneralId(99))); // 군주는 늘 남는다
        }
    }

    [Fact]
    public void 배신한_태수는_그_도시_태수직에서도_해제된다()
    {
        var s = State(cityGold: 100000, new[] { Gen(1, 0) }); // 충성 0 → 배신 확률 100%
        s = s with { Cities = new List<City> { s.Cities.Single() with { Governor = new GeneralId(1) } } };

        var after = World(3).AdvanceDays(s, GameState.DaysPerMonth);

        Assert.Null(after.PostingOf(new GeneralId(1)));       // 재야화
        Assert.Null(after.Cities.Single().Governor);          // 태수직 해제
    }
}
