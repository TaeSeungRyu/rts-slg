namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

/// <summary>성 전투 단계 머신(design-combat.md "성 전투") 검산 고정.</summary>
public class SiegeResolverTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> Templates =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(t => t.Code);

    private static readonly BattleResolver Resolver = new(multiTargetSecondaryPercent: 60);

    private static SiegeAttacker AttackerA(string code, int troops = 10000, bool inCounterRange = true)
    {
        var t = Templates[code];
        return new SiegeAttacker(troops, t.AtkBuilding, t.AtkUnit, t.Df, AptitudePercent: 95, InCounterRange: inCounterRange);
    }

    [Fact]
    public void 성벽단계_궁병3부대_성벽1425흡수_반격1187_712_712()
    {
        // design-combat 검산: A급 궁병 3부대×1만 vs A급 1만 성 → 성벽 -1,425, 반격 주 1187 / 나머지 712
        var attackers = new[] { AttackerA("archer"), AttackerA("archer"), AttackerA("archer") };
        var castle = new CastleState(WallCurrent: 6000, Troops: 10000, AptitudePercent: 95);

        var r = Resolver.ResolveSiege(attackers, castle);

        Assert.True(r.WallStanding);
        Assert.Equal(1425, r.WallDamage);           // 475 × 3
        Assert.Equal(4575, r.NewWall);
        Assert.Equal(0, r.TroopDamage);             // 성벽이 다 흡수
        Assert.Equal(new[] { 1187, 712, 712 }, r.CounterDamage);
    }

    [Fact]
    public void 성벽단계_벽력거1만_성벽1187흡수_병력무손실()
    {
        // 벽력거 건물dmg 15, 성 df 12 → 1,187 성벽 흡수. 반격은 벽력거 df 12로 791(문서 -950은 df 격상 전 값).
        var castle = new CastleState(WallCurrent: 6000, Troops: 10000, AptitudePercent: 95);
        var r = Resolver.ResolveSiege(new[] { AttackerA("thunder_cart") }, castle);

        Assert.Equal(1187, r.WallDamage);
        Assert.Equal(0, r.TroopDamage);
        Assert.Equal(791, r.CounterDamage[0]);
    }

    [Fact]
    public void 성벽단계_초과피해는_병력으로넘어간다()
    {
        // 성벽 1000 남았는데 벽력거 1187 → 1000 흡수, 187 병력으로, 성벽 0
        var castle = new CastleState(WallCurrent: 1000, Troops: 10000, AptitudePercent: 95);
        var r = Resolver.ResolveSiege(new[] { AttackerA("thunder_cart") }, castle);

        Assert.Equal(1000, r.WallDamage);
        Assert.Equal(0, r.NewWall);
        Assert.Equal(187, r.TroopDamage);
    }

    [Fact]
    public void 사거리2_공성탑은_성반격을_받지않는다()
    {
        var castle = new CastleState(WallCurrent: 6000, Troops: 10000, AptitudePercent: 95);
        var r = Resolver.ResolveSiege(new[] { AttackerA("siege_tower", inCounterRange: false) }, castle);

        Assert.True(r.WallDamage > 0);
        Assert.Equal(0, r.CounterDamage[0]); // 반격 없음
    }

    [Fact]
    public void 붕괴단계_유닛dmg가_병력직격하고_df6격하()
    {
        // 성벽 0. 도검(유닛dmg 8) vs 성 붕괴 df 6 → 1만·8·95÷(1000·6) = 1266
        var castle = new CastleState(WallCurrent: 0, Troops: 10000, AptitudePercent: 95);
        var r = Resolver.ResolveSiege(new[] { AttackerA("swordsman") }, castle);

        Assert.False(r.WallStanding);
        Assert.Equal(1266, r.TroopDamage);
        // 붕괴 반격(단독이라 분할 share=1): 1만·10·95÷(1000·도검df 10) = 950
        Assert.Equal(950, r.CounterDamage[0]);
    }
}
