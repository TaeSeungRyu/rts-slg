namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>교전 정산 순서(방어 → 회복 → 계략 → 공격) 통합 검증.</summary>
public class EngagementResolverTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly IReadOnlyDictionary<string, ActiveSkill> A =
        new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly EngagementResolver Engine = new(new BattleResolver(60));

    private static CombatStats SwordA(int troops = 10000)
        => CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.River, troops);

    private static Combatant Sword(int might = 60, int intellect = 60, int troops = 10000,
        ActiveSkill? strike = null, ActiveSkill? defense = null, ActiveSkill? heal = null)
        => new(SwordA(troops), MaxTroops: 10000, might, intellect, strike, defense, heal);

    [Fact]
    public void 평타대평타_양쪽760()
    {
        var r = Engine.Resolve(Sword(), Sword());
        Assert.Equal((760, 760, 0, 0), (r.DamageToA, r.DamageToB, r.HealA, r.HealB));
    }

    [Fact]
    public void 무쌍무력80_상대에게1459_받는건760()
    {
        var r = Engine.Resolve(Sword(might: 80, strike: A["peerless"]), Sword());
        Assert.Equal(1459, r.DamageToB);
        Assert.Equal(760, r.DamageToA);
    }

    [Fact]
    public void 철벽방어_받는피해_64퍼센트로_감소()
    {
        // A 무쌍(무80) vs B 철벽(무80): B가 받는 1459 × 0.64 = 933
        var r = Engine.Resolve(Sword(might: 80, strike: A["peerless"]), Sword(might: 80, defense: A["iron_wall"]));
        Assert.Equal(933, r.DamageToB);
        Assert.Equal(760, r.DamageToA);
    }

    [Fact]
    public void 회복은_공격전에_병력을늘려_딜이증가한다()
    {
        // A 정비(지력80) → 1800 회복 → 병력 9800으로 공격: 9800·8·95÷(1000·10) = 744
        var r = Engine.Resolve(Sword(troops: 8000, intellect: 80, heal: A["regroup"]), Sword());
        Assert.Equal(1800, r.HealA);
        Assert.Equal(744, r.DamageToB); // 늘어난 병력으로 더 때린다
        Assert.Equal(760, r.DamageToA);
    }

    [Fact]
    public void 회복은_최대병력을_넘기지_않는다()
    {
        var r = Engine.Resolve(Sword(intellect: 80, heal: A["regroup"]), Sword());

        Assert.Equal(0, r.HealA);
        Assert.Equal(760, r.DamageToB);
    }

    [Fact]
    public void 정산순서_불변_A와B를바꿔도_대칭()
    {
        var a = Sword(might: 80, strike: A["peerless"]);
        var b = Sword(might: 80, defense: A["iron_wall"]);
        var ab = Engine.Resolve(a, b);
        var ba = Engine.Resolve(b, a);
        Assert.Equal(ab.DamageToB, ba.DamageToA);
        Assert.Equal(ab.DamageToA, ba.DamageToB);
    }
}
