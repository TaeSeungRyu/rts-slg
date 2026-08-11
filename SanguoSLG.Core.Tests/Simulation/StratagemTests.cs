namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>계략 시스템(design-stratagem.md): 강도·숙달·모략력·지형·피해.</summary>
public class StratagemTests
{
    private static readonly IReadOnlyDictionary<string, Stratagem> St =
        new StratagemLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    [Fact]
    public void 로드_계략_11종()
        => Assert.Equal(11, St.Count);

    [Theory]
    [InlineData(100, 60, 140)]  // 지력차 +40 → 140%
    [InlineData(60, 100, 60)]   // -40 → 60%
    [InlineData(200, 0, 200)]   // 상한
    [InlineData(0, 200, 30)]    // 하한
    public void 강도배율_지력차(int caster, int target, int expected)
        => Assert.Equal(expected, StratagemStrength.Percent(caster, target));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(84, 8)]
    [InlineData(85, 9)]   // Lv9 = 누적 85
    [InlineData(150, 9)]  // 전설 지장 시작 예시
    [InlineData(285, 10)] // Lv10 = 누적 285
    public void 숙달_누적포인트로_레벨판정(int points, int expectedLevel)
        => Assert.Equal(expectedLevel, StratagemMastery.LevelFromPoints(points));

    [Fact]
    public void 숙달_9에서10은_200회()
        => Assert.Equal(200, StratagemMastery.NextLevelCost(9));

    [Fact]
    public void 낙뢰_필요단계10_레벨9면_잠금()
    {
        Assert.False(StratagemMastery.IsUnlocked(St["lightning"].RequiredLevel, currentLevel: 9));
        Assert.True(StratagemMastery.IsUnlocked(St["lightning"].RequiredLevel, currentLevel: 10));
    }

    [Fact]
    public void 모략력_지력100이면_화계6회_낙뢰2회()
    {
        var pool = StratagemResource.FromIntellect(100);
        Assert.Equal(100, pool.Max);
        // 낙뢰(45) 2회 후 세 번째는 불가
        pool = pool.Spend(45).Spend(45);
        Assert.Equal(10, pool.Current);
        Assert.False(pool.CanSpend(45));
        // 성 복귀로 충전
        Assert.Equal(100, pool.Refill().Current);
    }

    [Fact]
    public void 낙뢰_즉발_병력25퍼센트_강도등호()
    {
        // 낙뢰 base 25%, 지력 동수(강도 100) → 1만의 25% = 2500
        Assert.Equal(2500, St["lightning"].Damage(10000, casterIntellect: 80, targetIntellect: 80));
    }

    [Fact]
    public void 화계_지속tick_강도반영()
    {
        // 화계 base 3%/진행, 지력차 +40(강도 140) → 3×1.4 = 4.2% → 1만의 4.2% = 420 (tick당)
        Assert.Equal(420, St["fire_plot"].Damage(10000, casterIntellect: 100, targetIntellect: 60));
    }

    [Fact]
    public void 화계_소하천이면_불가_그외_가능()
    {
        Assert.False(St["fire_plot"].CanCastOn(TerrainType.River));
        Assert.True(St["fire_plot"].CanCastOn(TerrainType.Plains));
    }

    [Fact]
    public void 수공_소하천만_가능()
    {
        Assert.True(St["flood_plot"].CanCastOn(TerrainType.River));
        Assert.False(St["flood_plot"].CanCastOn(TerrainType.Plains));
    }

    [Fact]
    public void 디버프계략은_피해0()
        => Assert.Equal(0, St["confound"].Damage(10000, 100, 60));
}
