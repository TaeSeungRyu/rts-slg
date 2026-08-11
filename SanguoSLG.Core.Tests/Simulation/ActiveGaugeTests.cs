namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Simulation;
using Xunit;

/// <summary>액티브 5일 충전 게이지(design-skill-actives.md).</summary>
public class ActiveGaugeTests
{
    [Fact]
    public void 새게이지는_준비안됨()
        => Assert.False(new ActiveGauge().IsReady);

    [Fact]
    public void 야전_5일누적되면_준비됨()
    {
        Assert.False(new ActiveGauge().Tick(4).IsReady);
        Assert.True(new ActiveGauge().Tick(4).Tick(1).IsReady);
    }

    [Fact]
    public void 긴진행으로_한번에_문턱을넘어도_준비됨()
        => Assert.True(new ActiveGauge(ElapsedDays: 3).Tick(4).IsReady); // 7일

    [Fact]
    public void 발동하면_1회소비로_0초기화()
    {
        var g = new ActiveGauge(ElapsedDays: 6);
        Assert.True(g.IsReady);
        Assert.Equal(0, g.Fire().ElapsedDays);
        Assert.False(g.Fire().IsReady);
    }

    [Fact]
    public void 성복귀하면_0초기화()
        => Assert.Equal(0, new ActiveGauge(ElapsedDays: 4).Reset().ElapsedDays);

    [Fact]
    public void 음수_경과일틱은_무시()
        => Assert.Equal(5, new ActiveGauge(5).Tick(0).ElapsedDays);
}
