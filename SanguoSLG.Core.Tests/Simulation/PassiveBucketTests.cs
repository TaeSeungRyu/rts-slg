namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>패시브 가산 버킷 평가(design-skill-passives.md · design-combat ③).</summary>
public class PassiveBucketTests
{
    private static readonly IReadOnlyDictionary<string, PassiveSkill> P =
        new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly BattleResolver Resolver = new(60);

    private static (int Atk, int Df) Eval(CombatContext ctx, params (string Code, int Tier)[] held)
        => PassiveBucketEvaluator.Evaluate(held.Select(h => (P[h.Code], h.Tier)), ctx);

    [Fact]
    public void 로드_패시브_40종()
        => Assert.Equal(40, P.Count);

    [Fact]
    public void 맹공_단계별_공격가산()
    {
        Assert.Equal((104, 100), Eval(new CombatContext(), ("fierce_assault", 1)));
        Assert.Equal((108, 100), Eval(new CombatContext(), ("fierce_assault", 2)));
        Assert.Equal((112, 100), Eval(new CombatContext(), ("fierce_assault", 3)));
    }

    [Fact]
    public void 광전사_트레이드오프_공격증가_방어감소()
        => Assert.Equal((125, 85), Eval(new CombatContext(), ("berserker", 3)));

    [Fact]
    public void 배수진_병력절반이하면_공격40증가_초과면_감소()
    {
        Assert.Equal((140, 100), Eval(new CombatContext(HpRatioPercent: 30), ("last_stand", 3)));
        Assert.Equal((90, 100), Eval(new CombatContext(HpRatioPercent: 80), ("last_stand", 3)));
    }

    [Fact]
    public void 백병_인접교전에서만_발동()
    {
        Assert.Equal((112, 100), Eval(new CombatContext(MeleeEngagement: true), ("melee_master", 3)));
        Assert.Equal((100, 100), Eval(new CombatContext(MeleeEngagement: false), ("melee_master", 3)));
    }

    [Fact]
    public void 수성_성주둔에서만_방어25()
    {
        Assert.Equal((100, 125), Eval(new CombatContext(InCastle: true), ("castle_defender", 3)));
        Assert.Equal((100, 100), Eval(new CombatContext(InCastle: false), ("castle_defender", 3)));
    }

    [Fact]
    public void 두장수_패시브가_모두합산된다()
    {
        // 선봉 맹공(공+12) + 부관 견수(방+12)
        Assert.Equal((112, 112), Eval(new CombatContext(), ("fierce_assault", 3), ("steadfast_guard", 3)));
    }

    [Fact]
    public void 통합_맹공3단계_교전피해가_12퍼센트높다()
    {
        var (atk, _) = Eval(new CombatContext(), ("fierce_assault", 3));
        var attacker = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.River, 10000, atkBonusPercent: atk);
        var defender = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.River, 10000);
        Assert.Equal(851, Resolver.Damage(attacker, defender)); // 760 × 1.12
    }
}
