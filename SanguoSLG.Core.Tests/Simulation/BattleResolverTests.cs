namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

/// <summary>야전 교전·다대일 정산(design-combat.md "전투 페이즈"·"야전 다대일").</summary>
public class BattleResolverTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> Templates =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(t => t.Code);

    private static readonly BattleResolver Resolver = new(multiTargetSecondaryPercent: 60);

    // A급 부대(적성 95)를 병종 코드로 만든다.
    private static CombatStats UnitA(string code, int troops = 10000)
    {
        var t = Templates[code];
        return new CombatStats(troops, t.AtkUnit, t.Df, AptitudePercent: 95);
    }

    [Fact]
    public void Exchange_도검A대도검A_양쪽760()
    {
        var (toA, toB) = Resolver.Exchange(UnitA("swordsman"), UnitA("swordsman"));
        Assert.Equal(760, toA);
        Assert.Equal(760, toB);
    }

    [Fact]
    public void Exchange_상병대도검_상병이더주고덜받는다()
    {
        // 지수 상병 196 > 도검 80 — 상병이 우위
        var (toElephant, toSword) = Resolver.Exchange(UnitA("war_elephant"), UnitA("swordsman"));
        Assert.Equal(542, toElephant);   // 도검→상병: 1만·8·95÷(1000·14)
        Assert.Equal(1330, toSword);     // 상병→도검: 1만·14·95÷(1000·10)
        Assert.True(toSword > toElephant);
    }

    [Fact]
    public void Damage_부차대상은_60퍼센트()
    {
        var attacker = UnitA("swordsman");
        var target = UnitA("swordsman");
        Assert.Equal(760, Resolver.Damage(attacker, target, primaryTarget: true));
        Assert.Equal(456, Resolver.Damage(attacker, target, primaryTarget: false)); // 760 × 0.6
    }

    [Fact]
    public void DamageManyTargets_주대상100_나머지60()
    {
        // 1:3 — 도검 A가 도검 3부대와 교전(design-combat 케이스2): 100% / 60% / 60%
        var attacker = UnitA("swordsman");
        var targets = new[] { UnitA("swordsman"), UnitA("swordsman"), UnitA("swordsman") };
        var dmg = Resolver.DamageManyTargets(attacker, targets);
        Assert.Equal(new[] { 760, 456, 456 }, dmg);
    }

    [Fact]
    public void Damage_방어보너스가_피해를낮춘다()
    {
        var attacker = UnitA("swordsman");
        var defender = UnitA("swordsman") with { DfBonusPercent = 124 }; // 방어 +24%
        Assert.Equal(612, Resolver.Damage(attacker, defender));
    }
}
