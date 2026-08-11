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
        UnitCombatState? cs = null, int might = 60, HexCoord? target = null)
    {
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos, 2, 2, 1,
            MovementDomain.Land, mode, target, 0);
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

    private static CombatUnit Archer(int id, int owner, HexCoord pos, UnitCombatState? cs = null)
    {
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos, 2, 2, 2,
            MovementDomain.Land, UnitMode.Attack, null, 0);
        var stats = CombatStatsBuilder.BuildField(T["archer"], AptitudeGrade.A, 0, TerrainType.River, 10000);
        return new CombatUnit(field, stats, new TroopPool(10000, 0),
            cs ?? UnitCombatState.Create(60), 60, Intellect: 60, MaxTroops: 10000);
    }

    [Fact]
    public void 수공_공격감소_디버프가_준피해를_줄인다()
    {
        // 공격 −20% 디버프를 가진 부대는 준 피해가 760 → 608(×0.8)로 준다.
        var atkDown = new StatusEffect(StatusKind.AttackDown, 0, 2, false, AtkDownPercent: 20);
        var a = Sword(1, 1, new HexCoord(0, 0), cs: UnitCombatState.Create(60).AddStatus(atkDown));
        var b = Sword(2, 2, new HexCoord(1, 0));
        var turn = MakeOrchestrator().Run(new[] { a, b });

        var ub = turn.Units.Single(u => u.Id.Value == 2);
        var ua = turn.Units.Single(u => u.Id.Value == 1);
        Assert.Equal(10000 - 608, ub.Pool.Active); // a의 준 피해 20% 감소
        Assert.Equal(10000 - 760, ua.Pool.Active);  // b는 정상 반격
    }

    [Fact]
    public void 연막_원거리디버프는_근접부대에는_영향없다()
    {
        // 연막(원거리 한정)을 근접(사거리1) 부대가 지녀도 준 피해는 그대로.
        var rangedDown = new StatusEffect(StatusKind.RangedDown, 0, 2, false, AtkDownPercent: 30, RangedOnly: true);
        var a = Sword(1, 1, new HexCoord(0, 0), cs: UnitCombatState.Create(60).AddStatus(rangedDown));
        var b = Sword(2, 2, new HexCoord(1, 0));
        var turn = MakeOrchestrator().Run(new[] { a, b });

        var ub = turn.Units.Single(u => u.Id.Value == 2);
        Assert.Equal(10000 - 760, ub.Pool.Active); // 감소 없음
    }

    [Fact]
    public void 연막_원거리디버프는_궁병_준피해를_30퍼센트_줄인다()
    {
        var rangedDown = new StatusEffect(StatusKind.RangedDown, 0, 2, false, AtkDownPercent: 30, RangedOnly: true);
        var orch = MakeOrchestrator();

        var baseTurn = orch.Run(new[] { Archer(1, 1, new HexCoord(0, 0)), Sword(2, 2, new HexCoord(1, 0)) });
        var baseLoss = 10000 - baseTurn.Units.Single(u => u.Id.Value == 2).Pool.Active;

        var smokeArcher = Archer(1, 1, new HexCoord(0, 0), UnitCombatState.Create(60).AddStatus(rangedDown));
        var smokeTurn = orch.Run(new[] { smokeArcher, Sword(2, 2, new HexCoord(1, 0)) });
        var smokeLoss = 10000 - smokeTurn.Units.Single(u => u.Id.Value == 2).Pool.Active;

        Assert.Equal(baseLoss * 70 / 100, smokeLoss);
    }

    [Fact]
    public void 이간_무효디버프는_적성패시브를_없애_준피해를_줄인다()
    {
        // 가산 버킷 +100%를 가진 부대: 무효(이간)가 걸리면 적성·버킷이 100으로 돌아가 준 피해가 준다.
        var boosted = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.River, 10000)
            with { AtkBonusPercent = 200 };
        var orch = MakeOrchestrator();

        var control = Sword(1, 1, new HexCoord(0, 0)) with { Stats = boosted };
        var baseLoss = 10000 - orch.Run(new[] { control, Sword(2, 2, new HexCoord(1, 0)) })
            .Units.Single(u => u.Id.Value == 2).Pool.Active;

        var nullify = new StatusEffect(StatusKind.Nullify, 0, 2, false, NullifyAptPassive: true);
        var disrupted = (Sword(1, 1, new HexCoord(0, 0), cs: UnitCombatState.Create(60).AddStatus(nullify)))
            with { Stats = boosted };
        var nullLoss = 10000 - orch.Run(new[] { disrupted, Sword(2, 2, new HexCoord(1, 0)) })
            .Units.Single(u => u.Id.Value == 2).Pool.Active;

        Assert.True(nullLoss < baseLoss, $"무효 후 준 피해({nullLoss})가 원래({baseLoss})보다 작아야 함");
    }

    [Fact]
    public void 혼란_행동불가_부대는_공격하지_못한다()
    {
        var daze = new StatusEffect(StatusKind.Daze, 0, 3, false);
        var a = Sword(1, 1, new HexCoord(0, 0), cs: UnitCombatState.Create(60).AddStatus(daze));
        var b = Sword(2, 2, new HexCoord(1, 0));
        var turn = MakeOrchestrator().Run(new[] { a, b });

        var ua = turn.Units.Single(u => u.Id.Value == 1);
        var ub = turn.Units.Single(u => u.Id.Value == 2);
        Assert.Equal(10000, ub.Pool.Active);       // a는 행동불가 → 공격 못 함
        Assert.Equal(10000 - 760, ua.Pool.Active);  // b는 정상 공격(a는 피격·무반격)
    }

    [Fact]
    public void 혼란_행동불가_부대는_이동하지_못한다()
    {
        var daze = new StatusEffect(StatusKind.Daze, 0, 3, false);
        var a = Sword(1, 1, new HexCoord(0, 0), UnitMode.Attack,
            cs: UnitCombatState.Create(60).AddStatus(daze), target: new HexCoord(5, 0));
        var turn = MakeOrchestrator().Run(new[] { a });

        Assert.Equal(new HexCoord(0, 0), turn.Units[0].Field.Position); // 제자리
        Assert.Null(turn.Combat);
    }

    [Fact]
    public void 수공_이동감소_부대는_한칸씩_덜_간다()
    {
        var flood = new StatusEffect(StatusKind.AttackDown, 0, 2, false, AtkDownPercent: 20, MoveDownTiles: 1);
        var orch = MakeOrchestrator();

        var control = Sword(1, 1, new HexCoord(0, 0), UnitMode.Advance, target: new HexCoord(14, 0));
        var slowed = Sword(2, 2, new HexCoord(0, 0), UnitMode.Advance,
            cs: UnitCombatState.Create(60).AddStatus(flood), target: new HexCoord(14, 0));

        // 속도 2 × 7일 = 14칸(목표 도달) vs 이동 −1 → 속도 1 × 7일 = 7칸
        Assert.Equal(new HexCoord(14, 0), orch.Run(new[] { control }).Units[0].Field.Position);
        Assert.Equal(new HexCoord(7, 0), orch.Run(new[] { slowed }).Units[0].Field.Position);
    }

    [Fact]
    public void 수공_발동은_즉발15퍼센트와_공격이동디버프를_함께건다()
    {
        var map = new HexMap(0, 20, -5, 5);
        var movement = new MovementSimulator(new PassabilityMap(map, [], []));
        var orch = new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70),
            terrainAt: _ => TerrainType.River); // 수공은 소하천에서만 발동

        var casterState = UnitCombatState.Create(60)
            .ReserveStratagem(St["flood_plot"], new UnitId(2))
            .AdvanceField(2);
        var caster = Sword(1, 1, new HexCoord(0, 0), UnitMode.March, casterState);
        var target = Sword(2, 2, new HexCoord(2, 0), UnitMode.March); // 거리2: 교전없음, 계략 사거리2

        var turn = orch.Run(new[] { caster, target });
        var ut = turn.Units.Single(u => u.Id.Value == 2);

        Assert.Equal(10000 - 1500, ut.Pool.Active); // 즉발 15%(강도 100)
        Assert.Equal(1500, turn.StratagemDamage[new UnitId(2)]);
        var s = Assert.Single(ut.State.Statuses);
        Assert.Equal(StatusKind.AttackDown, s.Kind);
        Assert.Equal(20, s.AtkDownPercent);
        Assert.Equal(1, s.MoveDownTiles);
    }

    [Fact]
    public void 폭파_광역_대상과_인접한_적전원을_때리되_먼적과_아군은_뺀다()
    {
        // 시전자(1). 대상(2) 사거리 안. 대상 인접 적(3)·먼 적(4)·대상 인접 아군(5).
        var casterState = UnitCombatState.Create(60)
            .ReserveStratagem(St["detonate"], new UnitId(2))
            .AdvanceField(2);
        var caster = Sword(1, 1, new HexCoord(0, 0), UnitMode.March, casterState);
        var target = Sword(2, 2, new HexCoord(2, 0), UnitMode.March);
        var nearEnemy = Sword(3, 2, new HexCoord(3, 0), UnitMode.March);   // 대상서 거리 1
        var farEnemy = Sword(4, 2, new HexCoord(5, 0), UnitMode.March);    // 대상서 거리 3
        var nearAlly = Sword(5, 1, new HexCoord(2, 1), UnitMode.March);    // 대상서 거리 1(아군)

        var turn = MakeOrchestrator().Run(new[] { caster, target, nearEnemy, farEnemy, nearAlly });
        CombatUnit U(int id) => turn.Units.Single(u => u.Id.Value == id);

        Assert.Equal(10000 - 600, U(2).Pool.Active); // 대상 6%
        Assert.Equal(10000 - 600, U(3).Pool.Active); // 인접 적 6%
        Assert.Equal(10000, U(4).Pool.Active);        // 먼 적 무피해
        Assert.Equal(10000, U(5).Pool.Active);        // 아군 무피해
        Assert.Equal(10000, U(1).Pool.Active);        // 시전자 무피해
        Assert.Equal(600, turn.StratagemDamage[new UnitId(2)]);
        Assert.Equal(600, turn.StratagemDamage[new UnitId(3)]);
        Assert.False(turn.StratagemDamage.ContainsKey(new UnitId(4)));
    }

    [Fact]
    public void 교란_강제후퇴_대상을_시전자에게서_밀어낸다()
    {
        // 시전자(1) 서쪽, 대상(2) 동쪽 인접. 교란 발동 → 즉발 5% + 후퇴 3칸(동쪽으로).
        var casterState = UnitCombatState.Create(60)
            .ReserveStratagem(St["rout"], new UnitId(2))
            .AdvanceField(2);
        var caster = Sword(1, 1, new HexCoord(0, 0), UnitMode.March, casterState);
        var target = Sword(2, 2, new HexCoord(1, 0), UnitMode.March);

        var turn = MakeOrchestrator().Run(new[] { caster, target });
        var ut = turn.Units.Single(u => u.Id.Value == 2);

        Assert.Equal(new HexCoord(4, 0), ut.Field.Position); // (1,0)에서 3칸 밀림
        Assert.Equal(10000 - 500, ut.Pool.Active);            // 즉발 5%(강도 100)
        Assert.Equal(500, turn.StratagemDamage[new UnitId(2)]);
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
