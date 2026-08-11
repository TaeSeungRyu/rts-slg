namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>전투 페이즈 정산 — 다대일 누적·행군 70%·동시 스냅샷·TroopPool 적용.</summary>
public class CombatPhaseResolverTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly CombatPhaseResolver Phase = new(new BattleResolver(60), woundedPercent: 70);

    private static FieldUnit FUnit(int id, int owner, HexCoord pos, UnitMode mode, int range = 1, int cmd = 0)
        => new(new UnitId(id), new FactionId(owner), pos, 2, 2, range, MovementDomain.Land, mode, null, cmd);

    private static BattleParticipant SwordPart(UnitMode mode, int troops = 10000)
    {
        var stats = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.River, troops);
        return new BattleParticipant(stats, mode, new TroopPool(troops, 0));
    }

    [Fact]
    public void 일대일_양쪽760_받고_부상532()
    {
        var a = FUnit(1, 1, new HexCoord(0, 0), UnitMode.Attack);
        var b = FUnit(2, 2, new HexCoord(1, 0), UnitMode.Attack);
        var eng = CombatPhase.DetectEngagements(new[] { a, b });
        var parts = new Dictionary<UnitId, BattleParticipant>
        {
            [a.Id] = SwordPart(UnitMode.Attack),
            [b.Id] = SwordPart(UnitMode.Attack),
        };

        var r = Phase.Resolve(eng, parts);

        Assert.Equal(760, r.DamageTaken[a.Id]);
        Assert.Equal(760, r.DamageTaken[b.Id]);
        Assert.Equal(9240, r.Pools[a.Id].Active);
        Assert.Equal(532, r.Pools[a.Id].Wounded); // 760 × 70%
    }

    [Fact]
    public void 포위_1대3_주는건220퍼센트_받는건300퍼센트()
    {
        // A를 셋이 인접 포위(전부 거리 1). A가 셋을 침(주760+부456+부456), 셋이 각자 A를 침(760×3)
        var a = FUnit(1, 1, new HexCoord(0, 0), UnitMode.Attack);
        var b1 = FUnit(2, 2, new HexCoord(1, 0), UnitMode.Attack, cmd: 0);
        var b2 = FUnit(3, 2, new HexCoord(0, 1), UnitMode.Attack, cmd: 1);
        var b3 = FUnit(4, 2, new HexCoord(-1, 0), UnitMode.Attack, cmd: 2);
        var eng = CombatPhase.DetectEngagements(new[] { a, b1, b2, b3 });
        var parts = new[] { a, b1, b2, b3 }.ToDictionary(u => u.Id, u => SwordPart(u.Mode));

        var r = Phase.Resolve(eng, parts);

        Assert.Equal(2280, r.DamageTaken[a.Id]);  // 760 × 3 (받는 300%)
        Assert.Equal(760, r.DamageTaken[b1.Id]);  // 주대상 100%
        Assert.Equal(456, r.DamageTaken[b2.Id]);  // 60%
        Assert.Equal(456, r.DamageTaken[b3.Id]);  // 60%
    }

    [Fact]
    public void 행군방어자는_받는피해_70퍼센트_반격없음()
    {
        var a = FUnit(1, 1, new HexCoord(0, 0), UnitMode.Attack);
        var m = FUnit(2, 2, new HexCoord(1, 0), UnitMode.March);
        var eng = CombatPhase.DetectEngagements(new[] { a, m });
        var parts = new Dictionary<UnitId, BattleParticipant>
        {
            [a.Id] = SwordPart(UnitMode.Attack),
            [m.Id] = SwordPart(UnitMode.March),
        };

        var r = Phase.Resolve(eng, parts);

        Assert.Equal(532, r.DamageTaken[m.Id]);          // 760 × 70%
        Assert.False(r.DamageTaken.ContainsKey(a.Id));   // 행군은 반격 안 함
    }
}
