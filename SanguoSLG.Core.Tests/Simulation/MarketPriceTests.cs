namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>시장 시세 — 월별 계절성(9·10월 최저, 겨울 최고) + 랜덤 지터, seeded 결정론.</summary>
public class MarketPriceTests
{
    private static readonly BalanceConfig Bal = new(MonthlyTaxPerCity: 0);

    private static WorldEngine Engine(int seed) => new(Bal, random: new SeededRandomSource(seed));

    private static GameState State() => new(
        1, 190, new List<Faction>(),
        new List<City> { new(new CityId(1), "c1", new HexCoord(0, 0), new FactionId(1), 1000, CastleSize.Medium) },
        new List<General>());

    // Day = month×30에 그 달 말 틱이 돌아 시세가 갱신된다(next.Month 기준).
    private static int PriceAtMonth(int seed, int month) =>
        Engine(seed).AdvanceDays(State(), month * 30).MarketPricePercent;

    [Fact]
    public void 추수철_9월이_겨울_12월보다_싸다()
    {
        // 지터 ±15%라도 9월(70)과 12월(135)은 구간이 안 겹친다 → 항상 추수철이 싸다.
        Assert.True(PriceAtMonth(42, 9) < PriceAtMonth(42, 12));
        Assert.True(PriceAtMonth(7, 9) < PriceAtMonth(7, 12));
        Assert.True(PriceAtMonth(1234, 10) < PriceAtMonth(1234, 1));
    }

    [Fact]
    public void 시세는_시드가_같으면_결정론적이다()
    {
        Assert.Equal(PriceAtMonth(99, 9), PriceAtMonth(99, 9));
        Assert.Equal(Engine(99).AdvanceDays(State(), 360).MarketPricePercent,
            Engine(99).AdvanceDays(State(), 360).MarketPricePercent);
    }

    [Fact]
    public void 시세는_계절_배수_기준_지터_범위_안에_있다()
    {
        var p = PriceAtMonth(42, 9); // 계절 70 × [85..115]% = [59..80]
        Assert.InRange(p, 70 * 85 / 100, 70 * 115 / 100);
    }
}
