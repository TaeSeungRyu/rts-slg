namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>액티브 스킬 효과(design-skill-actives.md): 타격·방어·회복 + 무력/지력 스케일.</summary>
public class ActiveSkillTests
{
    private static readonly IReadOnlyDictionary<string, ActiveSkill> A =
        new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly BattleResolver Resolver = new(60);

    private static CombatStats SwordA(int troops = 10000)
        => CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.River, troops);

    [Theory]
    [InlineData(60, 100)]
    [InlineData(80, 120)]
    [InlineData(100, 140)]
    [InlineData(30, 70)]
    [InlineData(0, 50)] // 하한
    public void 스탯스케일_퍼센트(int stat, int expected)
        => Assert.Equal(expected, StatScale.Percent(stat));

    [Fact]
    public void 로드_액티브_24종()
        => Assert.Equal(24, A.Count);

    [Fact]
    public void 무쌍_무력80_평타의_1_9배()
    {
        // 도검 평타 760 × 1.6(무쌍) × 1.2(무력80) = 1459
        var dmg = Resolver.StrikeDamage(SwordA(), SwordA(), A["peerless"], might: 80);
        Assert.Equal(1459, dmg);
    }

    [Fact]
    public void 일섬_df30퍼센트감소_관통()
    {
        // df 10 → 7, 평타(atk8,df7)=1085 × 1.0 × 1.2 = 1302
        var dmg = Resolver.StrikeDamage(SwordA(), SwordA(), A["flash"], might: 80);
        Assert.Equal(1302, dmg);
    }

    [Fact]
    public void 참_병력비례처형_atk무관()
    {
        // 무력80: 5% × 1.2 = 6% (상한 10%). 1만 × 6% = 600
        var dmg = Resolver.StrikeDamage(SwordA(), SwordA(20000), A["reap"], might: 80);
        Assert.Equal(1200, dmg); // 2만 × 6%
    }

    [Fact]
    public void 분쇄_유닛대상이면_평타로_보류()
    {
        // 건물 아님 → 배수 200 무시하고 평타(760)
        var dmg = Resolver.StrikeDamage(SwordA(), SwordA(), A["crush"], might: 60, targetIsBuilding: false);
        Assert.Equal(760, dmg);
    }

    [Fact]
    public void 철벽_무력80_받는피해_64퍼센트()
        => Assert.Equal(64, BattleResolver.DamageTakenPercent(A["iron_wall"], might: 80)); // 30 × 1.2 = 36 감소

    [Fact]
    public void 방어감소_하한_최대75퍼센트()
    {
        // 사수 -50% × 1.5(무력110, M 상한) = -75% → 배수 25 (하한)
        Assert.Equal(25, BattleResolver.DamageTakenPercent(A["hold_the_line"], might: 110));
    }

    [Fact]
    public void 정비_지력80_병력15퍼센트회복_스케일()
    {
        // 15% × 1.2 = 18% of 1만 = 1800
        Assert.Equal(1800, BattleResolver.HealAmount(A["regroup"], intellect: 80, maxTroops: 10000));
    }

    [Fact]
    public void 회복_상한40퍼센트_초과안함()
    {
        // 불사 20% × 1.4(지력100) = 28% (상한 40 미만) → 2800
        Assert.Equal(2800, BattleResolver.HealAmount(A["second_wind"], intellect: 100, maxTroops: 10000));
    }
}
