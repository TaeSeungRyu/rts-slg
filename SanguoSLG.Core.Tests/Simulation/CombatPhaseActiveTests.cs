namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>전투 페이즈에 액티브(타격·방어·회복)를 정산 순서대로 반영(design-combat "정산 순서").</summary>
public class CombatPhaseActiveTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly IReadOnlyDictionary<string, ActiveSkill> A =
        new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly CombatPhaseResolver Phase = new(new BattleResolver(60), woundedPercent: 70);

    private static FieldUnit FUnit(int id, int owner, HexCoord pos, int range = 1, int cmd = 0)
        => new(new UnitId(id), new FactionId(owner), pos, 2, 2, range, MovementDomain.Land, UnitMode.Attack, null, cmd);

    private static BattleParticipant Sword(int troops = 10000, int wounded = 0, int might = 60, int intellect = 60,
        ActiveSkill? strike = null, ActiveSkill? defense = null, ActiveSkill? heal = null)
    {
        var stats = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.River, troops);
        return new BattleParticipant(stats, UnitMode.Attack, new TroopPool(troops, wounded),
            might, intellect, MaxTroops: 10000, strike, defense, heal);
    }

    private static CombatPhaseResult Run(BattleParticipant pa, BattleParticipant pb)
    {
        var a = FUnit(1, 1, new HexCoord(0, 0));
        var b = FUnit(2, 2, new HexCoord(1, 0));
        var eng = CombatPhase.DetectEngagements(new[] { a, b });
        return Phase.Resolve(eng, new Dictionary<UnitId, BattleParticipant> { [a.Id] = pa, [b.Id] = pb });
    }

    [Fact]
    public void 무쌍_주대상에게_대체공격()
    {
        var r = Run(Sword(might: 80, strike: A["peerless"]), Sword());
        Assert.Equal(1459, r.DamageTaken[new UnitId(2)]);
        Assert.Equal(760, r.DamageTaken[new UnitId(1)]);
    }

    [Fact]
    public void 철벽_받는피해_감소()
    {
        // A 무쌍(무80) vs B 철벽(무80): B 받는 1459 × 0.64 = 933
        var r = Run(Sword(might: 80, strike: A["peerless"]), Sword(might: 80, defense: A["iron_wall"]));
        Assert.Equal(933, r.DamageTaken[new UnitId(2)]);
        Assert.Equal(760, r.DamageTaken[new UnitId(1)]);
    }

    [Fact]
    public void 회복_부상풀에서_병력늘려_딜증가하고_풀에서회복()
    {
        // A 정비(지력80): 부상 2000 중 1800 회복 → 활성 11800으로 공격(896)
        var r = Run(Sword(troops: 8000, wounded: 2000, intellect: 80, heal: A["regroup"]), Sword());
        // 회복 데미지: 8000활성+1800회복=9800? 아니 — 정비 15%×1.2=18% of max1만=1800, 부상 2000이라 전액 1800 회복
        // 활성 8000+1800=9800으로 공격: 9800·8·95÷(1000·10)=744.8 → 744
        Assert.Equal(744, r.DamageTaken[new UnitId(2)]);
        // A 풀: 회복 후 활성 9800, 부상 200 → B에게 760 맞음 → 활성 9040, 부상 200+532
        Assert.Equal(9040, r.Pools[new UnitId(1)].Active);
        Assert.Equal(732, r.Pools[new UnitId(1)].Wounded); // 200 + 760×70%
    }

    [Fact]
    public void 액티브없으면_4c2와_동일한_평타()
    {
        var r = Run(Sword(), Sword());
        Assert.Equal(760, r.DamageTaken[new UnitId(1)]);
        Assert.Equal(760, r.DamageTaken[new UnitId(2)]);
    }
}
