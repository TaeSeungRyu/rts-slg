namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Simulation;
using Xunit;

/// <summary>피해 구성(소실 30% / 부상 70%)과 부상 풀 회복(design-combat.md "피해 구성").</summary>
public class TroopPoolTests
{
    [Fact]
    public void 피해_70퍼센트만_부상풀로_30퍼센트는소실()
    {
        var pool = new TroopPool(Active: 10000, Wounded: 0).TakeDamage(1000, woundedPercent: 70);
        Assert.Equal(9000, pool.Active);  // 활성은 전액 감소
        Assert.Equal(700, pool.Wounded);  // 그중 70%만 회복 가능
    }

    [Fact]
    public void 회복은_부상풀에서만_되돌린다()
    {
        var pool = new TroopPool(9000, 700).Heal(500);
        Assert.Equal(9500, pool.Active);
        Assert.Equal(200, pool.Wounded);
    }

    [Fact]
    public void 회복이_부상풀을_초과하면_풀까지만()
    {
        var pool = new TroopPool(9000, 700).Heal(1000);
        Assert.Equal(9700, pool.Active); // 700만 회복
        Assert.Equal(0, pool.Wounded);
    }

    [Fact]
    public void 평생_회복가능한_최대는_받은피해의_70퍼센트()
    {
        // 1000 피해 → 부상 700 → 전부 회복해도 원래 1만 중 9700까지만
        var pool = new TroopPool(10000, 0).TakeDamage(1000, 70).Heal(10000);
        Assert.Equal(9700, pool.Active);
        Assert.Equal(0, pool.Wounded);
    }

    [Fact]
    public void 피해가_활성병력을_넘으면_활성까지만_소실()
    {
        var pool = new TroopPool(500, 0).TakeDamage(1000, 70);
        Assert.Equal(0, pool.Active);
        Assert.Equal(350, pool.Wounded); // 실제 손실 500의 70%
    }
}
