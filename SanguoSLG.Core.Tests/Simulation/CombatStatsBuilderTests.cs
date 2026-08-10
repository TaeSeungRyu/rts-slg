namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>산출 ①~③ 조립(적성·연구·지형 → CombatStats) 검증.</summary>
public class CombatStatsBuilderTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly BattleResolver Resolver = new(60);

    [Theory]
    [InlineData(AptitudeGrade.F, 25)]
    [InlineData(AptitudeGrade.A, 95)]
    [InlineData(AptitudeGrade.APlus, 100)]
    [InlineData(AptitudeGrade.SS, 130)]
    [InlineData(AptitudeGrade.SSS, 200)]
    public void 적성등급_퍼센트매핑(AptitudeGrade grade, int expected)
        => Assert.Equal(expected, grade.Percent());

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(8, 8)]
    [InlineData(9, 10)]  // 9단계 누적 +10 = 옛 풀연구
    [InlineData(10, 13)] // 10단계 누적 +13
    public void 연구곡선_누적보정(int level, int expected)
        => Assert.Equal(expected, ResearchCurve.Bonus(level));

    [Fact]
    public void 지형보정_숲궁병_공방플러스2()
        => Assert.Equal((2, 2), TerrainCombatBonus.For(TroopClass.Archer, TerrainType.Forest));

    [Fact]
    public void 지형보정_평야기병_공격만플러스2()
        => Assert.Equal((2, 0), TerrainCombatBonus.For(TroopClass.Cavalry, TerrainType.Plains));

    [Fact]
    public void 지형보정_조건불일치면_0()
        => Assert.Equal((0, 0), TerrainCombatBonus.For(TroopClass.Infantry, TerrainType.Plains));

    [Fact]
    public void 빌드_도검A_무연구_평지_기본스탯()
    {
        var s = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.Plains, 10000);
        Assert.Equal((10000, 8, 10, 95), (s.Troops, s.AtkStat, s.DfStat, s.AptitudePercent));
    }

    [Fact]
    public void 빌드_도검_9단계연구_18_20()
    {
        // design-combat: 9단계 도검 18/20
        var s = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 9, TerrainType.Plains, 10000);
        Assert.Equal((18, 20), (s.AtkStat, s.DfStat));
    }

    [Fact]
    public void 빌드_궁병_숲이면_공방각2증가()
    {
        var s = CombatStatsBuilder.BuildField(T["archer"], AptitudeGrade.A, 0, TerrainType.Forest, 10000);
        Assert.Equal((12, 10), (s.AtkStat, s.DfStat)); // 유닛dmg 10+2, df 8+2
    }

    [Fact]
    public void 빌드_건물대상이면_건물dmg를쓴다()
    {
        var s = CombatStatsBuilder.BuildField(T["thunder_cart"], AptitudeGrade.A, 0, TerrainType.Plains, 10000, targetIsBuilding: true);
        Assert.Equal(15, s.AtkStat); // 벽력거 건물dmg 15
    }

    [Fact]
    public void 빌드_9단계도검_교전이_기본보다세다()
    {
        var researched = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 9, TerrainType.Plains, 10000);
        var baseline = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.Plains, 10000);
        // 9단계(atk18/df20) 도검이 무연구 도검을 친다: 1만·18·95÷(1000·10) = 1710
        Assert.Equal(1710, Resolver.Damage(researched, baseline));
    }
}
