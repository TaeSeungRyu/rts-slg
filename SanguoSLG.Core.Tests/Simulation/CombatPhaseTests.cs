namespace SanguoSLG.Core.Tests.Simulation;

using System.Linq;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>전투 페이즈 발동 — 사거리 전수검사·다대일 페어링(design-combat.md).</summary>
public class CombatPhaseTests
{
    private static FieldUnit Unit(int id, int owner, HexCoord pos, UnitMode mode,
        int attackRange = 1, int commandOrder = 0) =>
        new(new UnitId(id), new FactionId(owner), pos, 2, 2, attackRange,
            MovementDomain.Land, mode, null, commandOrder);

    [Fact]
    public void 사거리안_적대쌍이_서로_교전한다()
    {
        var a = Unit(1, owner: 1, new HexCoord(0, 0), UnitMode.Attack);
        var b = Unit(2, owner: 2, new HexCoord(1, 0), UnitMode.Attack);
        var eng = CombatPhase.DetectEngagements(new[] { a, b });

        Assert.Equal(2, eng.Count);
        Assert.Equal(new[] { new UnitId(2) }, eng.Single(e => e.Attacker.Value == 1).Targets);
        Assert.Equal(new[] { new UnitId(1) }, eng.Single(e => e.Attacker.Value == 2).Targets);
    }

    [Fact]
    public void 사거리밖이면_교전없음()
    {
        var a = Unit(1, 1, new HexCoord(0, 0), UnitMode.Attack, attackRange: 1);
        var b = Unit(2, 2, new HexCoord(3, 0), UnitMode.Attack, attackRange: 1);
        Assert.Empty(CombatPhase.DetectEngagements(new[] { a, b }));
    }

    [Fact]
    public void 아군은_대상이_아니다()
    {
        var a = Unit(1, 1, new HexCoord(0, 0), UnitMode.Attack);
        var ally = Unit(2, 1, new HexCoord(1, 0), UnitMode.Attack);
        Assert.Empty(CombatPhase.DetectEngagements(new[] { a, ally }));
    }

    [Fact]
    public void 행군모드는_공격하지않지만_대상은_된다()
    {
        // a(공격) 옆에 b(행군). a는 b를 치지만 b는 아무도 안 친다.
        var a = Unit(1, 1, new HexCoord(0, 0), UnitMode.Attack);
        var b = Unit(2, 2, new HexCoord(1, 0), UnitMode.March);
        var eng = CombatPhase.DetectEngagements(new[] { a, b });

        Assert.Single(eng);
        Assert.Equal(new UnitId(1), eng[0].Attacker);
        Assert.Equal(new[] { new UnitId(2) }, eng[0].Targets);
    }

    [Fact]
    public void 전진모드는_정지시점_사거리안이면_교전한다()
    {
        var a = Unit(1, 1, new HexCoord(0, 0), UnitMode.Advance);
        var b = Unit(2, 2, new HexCoord(1, 0), UnitMode.Attack);
        var eng = CombatPhase.DetectEngagements(new[] { a, b });

        Assert.Contains(eng, e => e.Attacker.Value == 1); // 전진도 공격자
    }

    [Fact]
    public void 다대일_주대상은_가까운_다음_명령순번()
    {
        // a가 사거리 2. 적 셋: b1(거리1), b2(거리2, 명령0), b3(거리2, 명령1) → 순서 b1, b2, b3
        var a = Unit(1, 1, new HexCoord(0, 0), UnitMode.Attack, attackRange: 2);
        var b1 = Unit(2, 2, new HexCoord(1, 0), UnitMode.Attack, commandOrder: 5);
        var b2 = Unit(3, 2, new HexCoord(2, 0), UnitMode.Attack, commandOrder: 0);
        var b3 = Unit(4, 2, new HexCoord(-2, 0), UnitMode.Attack, commandOrder: 1);

        var eng = CombatPhase.DetectEngagements(new[] { a, b1, b2, b3 })
            .Single(e => e.Attacker.Value == 1);

        Assert.Equal(new[] { new UnitId(2), new UnitId(3), new UnitId(4) }, eng.Targets);
    }
}
