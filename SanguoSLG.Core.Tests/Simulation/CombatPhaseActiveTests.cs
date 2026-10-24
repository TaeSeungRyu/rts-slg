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
    public void 맹호격은_상대의_이번_반격만_절반으로_줄인다()
    {
        var result = Run(Sword(strike: A["tiger_strike"]), Sword());
        Assert.Equal(1064, result.DamageTaken[new UnitId(2)]);
        Assert.Equal(380, result.DamageTaken[new UnitId(1)]);
    }

    [Theory]
    [InlineData("chain_strike")]
    [InlineData("double_hit")]
    public void 연속타격은_단일대상에_두_번의_교환으로_기록된다(string code)
    {
        var result = Run(Sword(strike: A[code]), Sword());
        var hits = result.Exchanges!.Where(exchange => exchange.Attacker == new UnitId(1)).ToList();
        Assert.Equal(2, hits.Count);
        Assert.Equal(result.DamageTaken[new UnitId(2)], hits.Sum(hit => hit.Amount));
    }

    [Fact]
    public void 연환격은_두_대상에게_각각_팔십퍼센트의_피해를_준다()
    {
        var participants = new Dictionary<UnitId, BattleParticipant>
        {
            [new UnitId(1)] = Sword(strike: A["chain_strike"]),
            [new UnitId(2)] = Sword(),
            [new UnitId(3)] = Sword(),
        };
        var result = Phase.Resolve([new UnitEngagement(new UnitId(1), [new UnitId(2), new UnitId(3)])],
            participants);
        Assert.Equal(608, result.DamageTaken[new UnitId(2)]);
        Assert.Equal(608, result.DamageTaken[new UnitId(3)]);
    }

    [Fact]
    public void 참은_공격보너스와_피해배수를_적용하고_일반부상을_남긴다()
    {
        var r = Run(Sword(might: 80, strike: A["reap"]), Sword());

        Assert.Equal(1313, r.DamageTaken[new UnitId(2)]);
        Assert.Equal(8687, r.Pools[new UnitId(2)].Active);
        Assert.Equal(919, r.Pools[new UnitId(2)].Wounded);
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
        // A 정비(지력80): 부상 2000 중 1200 회복 → 활성 9200으로 공격.
        var r = Run(Sword(troops: 8000, wounded: 2000, intellect: 80, heal: A["regroup"]), Sword());
        Assert.Equal(699, r.DamageTaken[new UnitId(2)]);
        Assert.Equal(8440, r.Pools[new UnitId(1)].Active);
        Assert.Equal(1332, r.Pools[new UnitId(1)].Wounded);
    }

    [Fact]
    public void 회복_최대병력을_넘지_않는다()
    {
        var r = Run(Sword(troops: 10000, wounded: 2000, intellect: 80, heal: A["regroup"]), Sword());

        Assert.Equal(9240, r.Pools[new UnitId(1)].Active);
    }

    [Fact]
    public void 회복_활성병력이_부족해도_부상병이_없으면_병력을_만들지_않는다()
    {
        var r = Run(Sword(troops: 8000, wounded: 0, intellect: 100, heal: A["second_wind"]), Sword());

        // 불사 회복량 자체는 충분하지만 부상 풀이 0이므로, 공격 전 활성 병력은 8,000 그대로다.
        Assert.Equal(608, r.DamageTaken[new UnitId(2)]);
        Assert.Equal(7240, r.Pools[new UnitId(1)].Active);
        Assert.Equal(532, r.Pools[new UnitId(1)].Wounded);
    }

    [Fact]
    public void 액티브없으면_4c2와_동일한_평타()
    {
        var r = Run(Sword(), Sword());
        Assert.Equal(760, r.DamageTaken[new UnitId(1)]);
        Assert.Equal(760, r.DamageTaken[new UnitId(2)]);
    }
}
