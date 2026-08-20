namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>사기·훈련(design-unit-state 2·3단계) — 공/방 배수, 증감, 패주.</summary>
public class MoraleTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static AdvanceOrchestrator Orchestrator() =>
        new(new MovementSimulator(new PassabilityMap(new HexMap(0, 20, -5, 5), [], [])),
            new CombatPhaseResolver(new BattleResolver(60), 70));

    private static CombatUnit Sword(int id, int owner, HexCoord pos, UnitMode mode = UnitMode.Attack,
        HexCoord? target = null, int morale = 50, int training = 50, bool routed = false, int troops = 10000)
    {
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos, 2, 2, 1,
            MovementDomain.Land, mode, target, id);
        var stats = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.River, troops);
        return new CombatUnit(field, stats, new TroopPool(troops, 0), UnitCombatState.Create(60),
            60, 60, troops, TroopClass.Infantry, Provisions: -1, ProvisionsCapacity: 300, IsSupply: false,
            Morale: morale, Training: training, Routed: routed);
    }

    private static int DamageWith(int morale, int training)
    {
        var atk = Sword(1, 1, new HexCoord(0, 0), morale: morale, training: training);
        var target = Sword(2, 2, new HexCoord(1, 0), mode: UnitMode.March); // 반격·손실 배제
        return Orchestrator().Run(new[] { atk, target }).Combat!.DamageDealt[new UnitId(1)];
    }

    [Fact]
    public void 사기높은부대가_더_큰_피해를_준다()
    {
        // 사기 100(공 +20%) > 사기 50(기준) > 사기 0(−20%).
        Assert.True(DamageWith(100, 50) > DamageWith(50, 50));
        Assert.True(DamageWith(50, 50) > DamageWith(0, 50));
    }

    [Fact]
    public void 훈련도낮으면_공격이_약하다()
    {
        // 훈련 0(공 −10%) < 훈련 50(기준) < 훈련 100(+10%).
        Assert.True(DamageWith(50, 0) < DamageWith(50, 50));
        Assert.True(DamageWith(50, 50) < DamageWith(50, 100));
    }

    [Fact]
    public void 교전해서_피해입으면_사기가_내린다()
    {
        // 서로 공격 → 둘 다 760 피해(7%) → 사기 −3(7×1/2 내림), 우세(<10% 손실) +5 → 순 +2.
        var a = Sword(1, 1, new HexCoord(0, 0));
        var b = Sword(2, 2, new HexCoord(1, 0));
        var turn = Orchestrator().Run(new[] { a, b });
        // 피해 −3 + 우세 +5 = +2 → 사기 52
        Assert.Equal(52, turn.Units.Single(u => u.Id.Value == 1).Morale);
    }

    [Fact]
    public void 적을_격파하면_사기가_오른다()
    {
        // 대상 병력을 1로 두면 한 진행에 전멸 → 격파 +10(+우세 +5, 무손실).
        var a = Sword(1, 1, new HexCoord(0, 0));
        var weak = Sword(2, 2, new HexCoord(1, 0), mode: UnitMode.March, troops: 1);
        var turn = Orchestrator().Run(new[] { a, weak });
        Assert.DoesNotContain(turn.Units, u => u.Id.Value == 2);            // 격파됨
        Assert.Equal(50 + 10 + 5, turn.Units.Single(u => u.Id.Value == 1).Morale); // 격파+우세
    }

    [Fact]
    public void 무전투_주둔이면_사기가_회복된다()
    {
        // 적 없이 목표 도착 → 휴식 +2.
        var a = Sword(1, 1, new HexCoord(0, 0), mode: UnitMode.March, target: new HexCoord(0, 0), morale: 40);
        var turn = Orchestrator().Run(new[] { a });
        Assert.Equal(42, turn.Units.Single().Morale);
    }

    [Fact]
    public void 사기가_임계밑이면_패주하고_적반대로_후퇴한다()
    {
        // 사기 5 부대가 인접 적에게서 강제 후퇴(공격 못 함). 패주 유지.
        var routed = Sword(1, 1, new HexCoord(5, 0), mode: UnitMode.Attack, target: new HexCoord(5, 0),
            morale: 5, routed: true);
        var enemy = Sword(2, 2, new HexCoord(6, 0), mode: UnitMode.March);
        var turn = Orchestrator().Run(new[] { routed, enemy });

        var r = turn.Units.Single(u => u.Id.Value == 1);
        Assert.True(r.Field.Position.Distance(new HexCoord(6, 0)) > 1, "적에게서 멀어져 후퇴");
        Assert.True(r.Routed);                       // 아직 패주(사기<40)
        Assert.Null(turn.Combat);                    // 패주 부대는 공격 안 함
    }

    [Fact]
    public void 패주한_부대는_명령_목표가_취소된다()
    {
        // 패주하면 목표를 지운다 — 도망친 뒤 사기를 회복해도 스스로 다시 진군하지 않는다.
        var routed = Sword(1, 1, new HexCoord(5, 0), mode: UnitMode.March, target: new HexCoord(10, 0),
            morale: 5, routed: true);
        var turn = Orchestrator().Run(new[] { routed });

        var u = turn.Units.Single();
        Assert.True(u.Routed);
        Assert.Null(u.Field.Target);
    }

    [Fact]
    public void 패주는_사기가_회복임계_이상이면_해제된다()
    {
        // 사기 38 패주 부대가 무전투 휴식(+2) → 40 도달 → 패주 해제.
        var r = Sword(1, 1, new HexCoord(0, 0), mode: UnitMode.March, target: new HexCoord(0, 0),
            morale: 38, routed: true);
        var turn = Orchestrator().Run(new[] { r });
        var u = turn.Units.Single();
        Assert.Equal(40, u.Morale);
        Assert.False(u.Routed);
    }
}
