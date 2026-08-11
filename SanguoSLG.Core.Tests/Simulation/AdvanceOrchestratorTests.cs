namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>진행 루프 오케스트레이터 — 이동 → 전투 페이즈 → 정산 통합(design-combat 순환).</summary>
public class AdvanceOrchestratorTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly IReadOnlyDictionary<string, ActiveSkill> A =
        new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static AdvanceOrchestrator MakeOrchestrator()
    {
        var map = new HexMap(0, 20, -5, 5);
        var movement = new MovementSimulator(new PassabilityMap(map, [], []));
        return new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), woundedPercent: 70));
    }

    private static CombatUnit Sword(int id, int owner, HexCoord pos, UnitMode mode = UnitMode.Attack,
        UnitCombatState? cs = null, int might = 60)
    {
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos, 2, 2, 1,
            MovementDomain.Land, mode, null, 0);
        var stats = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.River, 10000);
        return new CombatUnit(field, stats, new TroopPool(10000, 0),
            cs ?? UnitCombatState.Create(60), might, Intellect: 60, MaxTroops: 10000);
    }

    [Fact]
    public void 적없으면_이동만_전투없음()
    {
        var a = Sword(1, 1, new HexCoord(0, 0)) with
        {
            Field = new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(0, 0), 2, 2, 1,
                MovementDomain.Land, UnitMode.Advance, new HexCoord(4, 0), 0),
        };
        var turn = MakeOrchestrator().Run(new[] { a });

        Assert.Null(turn.Combat);
        Assert.Equal(new HexCoord(4, 0), turn.Units[0].Field.Position); // 목표 도달
    }

    [Fact]
    public void 인접_적대_부대가_교전해서_병력이_준다()
    {
        var a = Sword(1, 1, new HexCoord(0, 0));
        var b = Sword(2, 2, new HexCoord(1, 0));
        var turn = MakeOrchestrator().Run(new[] { a, b });

        Assert.NotNull(turn.Combat);
        var ua = turn.Units.Single(u => u.Id.Value == 1);
        var ub = turn.Units.Single(u => u.Id.Value == 2);
        Assert.Equal(9240, ua.Pool.Active);   // 760 피해
        Assert.Equal(532, ua.Pool.Wounded);   // 70% 부상
        Assert.Equal(9240, ub.Pool.Active);
    }

    private static readonly System.Collections.Generic.IReadOnlyDictionary<string, Stratagem> St =
        new StratagemLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    [Fact]
    public void 예약된_계략이_발동일에_터지고_시전부대는_공격을_건너뛴다()
    {
        // 시전 부대(1): 낙뢰(필요Lv10) 예약 후 2일 경과 → 발동일. 모략력 60·숙달 285(Lv10).
        var casterState = UnitCombatState.Create(60, masteryPoints: 285)
            .ReserveStratagem(St["lightning"], new UnitId(2))
            .AdvanceField(2);
        var caster = Sword(1, 1, new HexCoord(0, 0), cs: casterState);
        var target = Sword(2, 2, new HexCoord(1, 0));

        var turn = MakeOrchestrator().Run(new[] { caster, target });

        var uCaster = turn.Units.Single(u => u.Id.Value == 1);
        var uTarget = turn.Units.Single(u => u.Id.Value == 2);

        // 낙뢰 즉발 25%(지력 동수, 강도 100) = 2500 → 대상 병력 감소
        Assert.Equal(7500, uTarget.Pool.Active);
        Assert.Equal(1750, uTarget.Pool.Wounded);
        // 시전 부대는 공격 안 함 → 대상만 반격(줄어든 7500으로 570)
        Assert.Equal(9430, uCaster.Pool.Active);
        // 모략력 45 소비, 숙달 +1, 예약 해제
        Assert.Equal(15, uCaster.State.Resource.Current);
        Assert.Equal(286, uCaster.State.MasteryPoints);
        Assert.Null(uCaster.State.Reservation);
    }

    [Fact]
    public void 화계는_즉시피해없이_상태를걸고_다음진행부터_병력을_깎는다()
    {
        // 시전 부대(1): 화계 예약 후 2일 경과 → 발동일. 지력 100 vs 대상 60 → 강도 140.
        var casterState = UnitCombatState.Create(100, masteryPoints: 10)
            .ReserveStratagem(St["fire_plot"], new UnitId(2))
            .AdvanceField(2);
        // 둘 다 행군(정지·비교전) — 교전 없이 계략만 걸려 DoT를 격리한다. 거리 2(계략 사거리 2 도달).
        var caster = Sword(1, 1, new HexCoord(0, 0), UnitMode.March, casterState) with { Intellect = 100 };
        var target = Sword(2, 2, new HexCoord(2, 0), UnitMode.March);

        var orch = MakeOrchestrator();

        // 진행 1: 발동 → 상태만 부여(즉시 피해 없음), 교전 없음
        var t1 = orch.Run(new[] { caster, target });
        var tg1 = t1.Units.Single(u => u.Id.Value == 2);
        Assert.Equal(10000, tg1.Pool.Active);
        Assert.Empty(t1.StatusDamage);
        var burn = Assert.Single(tg1.State.Statuses);
        Assert.Equal(420, burn.TickBasisPoints); // 3% × 강도 140
        Assert.Equal(6, burn.Remaining);

        // 진행 2: 화상 tick — 420 피해(70% 부상)
        var t2 = orch.Run(t1.Units);
        var tg2 = t2.Units.Single(u => u.Id.Value == 2);
        Assert.Equal(420, t2.StatusDamage[new UnitId(2)]);
        Assert.Equal(9580, tg2.Pool.Active);
        Assert.Equal(294, tg2.Pool.Wounded);
        Assert.Equal(5, Assert.Single(tg2.State.Statuses).Remaining);
    }

    [Fact]
    public void 소화는_아군에게_걸린_화계상태를_제거한다()
    {
        var burned = UnitCombatState.Create(60).AddStatus(new StatusEffect(StatusKind.Burn, 420, 6, true));
        var casterState = UnitCombatState.Create(100, masteryPoints: 10)
            .ReserveStratagem(St["douse"], new UnitId(2))
            .AdvanceField(2);
        var caster = Sword(1, 1, new HexCoord(0, 0), cs: casterState);
        var target = Sword(2, 1, new HexCoord(2, 0), cs: burned); // 같은 세력(아군)

        var t = MakeOrchestrator().Run(new[] { caster, target });
        var tg = t.Units.Single(u => u.Id.Value == 2);

        // 발동 진행: 화상이 마지막으로 한 번 tick(420)한 뒤 제거된다.
        Assert.Equal(420, t.StatusDamage[new UnitId(2)]);
        Assert.Empty(tg.State.Statuses);
    }

    [Fact]
    public void 준비된_타격액티브가_교전에서_발동한다()
    {
        // 선봉 무쌍 게이지 준비(야전 5일 사전 누적), 무력 80
        var readyState = UnitCombatState.Create(60, vanguardActive: A["peerless"]).AdvanceField(5);
        var a = Sword(1, 1, new HexCoord(0, 0), cs: readyState, might: 80);
        var b = Sword(2, 2, new HexCoord(1, 0));
        var turn = MakeOrchestrator().Run(new[] { a, b });

        var ub = turn.Units.Single(u => u.Id.Value == 2);
        Assert.Equal(10000 - 1459, ub.Pool.Active); // 무쌍 무력80 = 1459
        // 발동한 선봉 게이지는 소비되어 준비 해제
        var ua = turn.Units.Single(u => u.Id.Value == 1);
        Assert.False(ua.State.VanguardGauge.IsReady);
    }
}
