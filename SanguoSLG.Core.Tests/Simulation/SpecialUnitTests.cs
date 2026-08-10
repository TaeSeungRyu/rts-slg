namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>특수 유닛 판정 전환·조건부 보정(design-combat.md "특수 유닛 추가 효과").</summary>
public class SpecialUnitTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly IReadOnlyDictionary<string, SpecialUnit> S =
        new SpecialUnitLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly BattleResolver Resolver = new(60);

    // 기본 지형은 어떤 분류에도 보정이 없는 소하천(River) — 지형 노이즈 없이 판정 전환만 본다.
    private static CombatStats Field(string special, TerrainType terrain = TerrainType.River, bool targetBuilding = false, bool impassable = false)
    {
        var su = S[special];
        return CombatStatsBuilder.BuildFieldSpecial(su, T[su.BaseCode], AptitudeGrade.A, 0, terrain, 10000, targetBuilding, impassable);
    }

    [Fact]
    public void 로드_특수유닛_8종()
        => Assert.Equal(8, S.Count);

    [Fact]
    public void 등갑병_df를_상병판정14로()
        => Assert.Equal(14, Field("deunggap").DfStat); // 기반 도검 df10 → 14

    [Fact]
    public void 왜선_df를_투석기판정4로()
        => Assert.Equal(4, Field("waeseon").DfStat); // 기반 소선 df6 → 4

    [Fact]
    public void 궁기병_건물공격은_궁병판정6_유닛공격은_기병판정12()
    {
        Assert.Equal(6, Field("horse_archer", targetBuilding: true).AtkStat);  // 기병 건물4 → 궁병 6
        Assert.Equal(12, Field("horse_archer").AtkStat);                       // 기병 유닛 12 그대로
    }

    [Fact]
    public void 화랑궁병_건물공격은_도검판정8()
    {
        Assert.Equal(8, Field("hwarang", targetBuilding: true).AtkStat);       // 궁병 건물6 → 도검 8
        Assert.Equal(10, Field("hwarang").AtkStat);                            // 궁병 유닛 10 그대로
    }

    [Fact]
    public void 철기병_모든공격_10퍼센트가산()
        => Assert.Equal(110, Field("cataphract").AtkBonusPercent);

    [Fact]
    public void 남만병_건물공격만_10퍼센트가산()
    {
        Assert.Equal(110, Field("namman", targetBuilding: true).AtkBonusPercent);
        Assert.Equal(100, Field("namman").AtkBonusPercent); // 유닛 상대엔 없음
    }

    [Fact]
    public void 무당비군_이동불가지형공격_10퍼센트가산()
    {
        Assert.Equal(110, Field("mudang", impassable: true).AtkBonusPercent);
        Assert.Equal(100, Field("mudang").AtkBonusPercent);
    }

    [Fact]
    public void 극병_기병상대_공격가산10_궁병에게_받는피해가산10()
    {
        // 극병(도검 기반)이 기병을 칠 때 +10
        var vsCav = CombatStatsBuilder.MatchupAtkBonus(S["geukbyeong"], TroopClass.Infantry, null, TroopClass.Cavalry);
        Assert.Equal(10, vsCav);

        // 궁병이 극병을 칠 때 공격자(궁병)가 +10 (= 극병 받는 피해 +10%)
        var archerVsGeuk = CombatStatsBuilder.MatchupAtkBonus(null, TroopClass.Archer, S["geukbyeong"], TroopClass.Infantry);
        Assert.Equal(10, archerVsGeuk);

        // 극병이 도검(보병)을 칠 땐 가산 없음
        var vsInf = CombatStatsBuilder.MatchupAtkBonus(S["geukbyeong"], TroopClass.Infantry, null, TroopClass.Infantry);
        Assert.Equal(0, vsInf);
    }

    [Fact]
    public void 극병_기병교전_피해가_10퍼센트높다()
    {
        var geuk = Field("geukbyeong");
        var geukVsCav = geuk with { AtkBonusPercent = geuk.AtkBonusPercent + CombatStatsBuilder.MatchupAtkBonus(S["geukbyeong"], TroopClass.Infantry, null, TroopClass.Cavalry) };
        var cavalry = CombatStatsBuilder.BuildField(T["cavalry"], AptitudeGrade.A, 0, TerrainType.Plains, 10000);
        var plainSword = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.Plains, 10000);

        var geukDmg = Resolver.Damage(geukVsCav, cavalry);
        var plainDmg = Resolver.Damage(plainSword, cavalry);
        Assert.Equal(696, geukDmg);   // 633 × 1.1
        Assert.Equal(633, plainDmg);  // 1만·8·95÷(1000·12)
    }
}
