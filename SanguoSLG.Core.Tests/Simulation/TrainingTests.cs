namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>훈련도(design-unit-state 3단계) — 공/방 배수. 사기 시스템은 2026-08-21 전면 폐지.</summary>
public class TrainingTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static AdvanceOrchestrator Orchestrator() =>
        new(new MovementSimulator(new PassabilityMap(new HexMap(0, 20, -5, 5), [], [])),
            new CombatPhaseResolver(new BattleResolver(60), 70));

    private static CombatUnit Sword(int id, int owner, HexCoord pos, UnitMode mode = UnitMode.Attack,
        HexCoord? target = null, int training = 50, int troops = 10000)
    {
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos, 2, 2, 1,
            MovementDomain.Land, mode, target, id);
        var stats = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.River, troops);
        return new CombatUnit(field, stats, new TroopPool(troops, 0), UnitCombatState.Create(60),
            60, 60, troops, TroopClass.Infantry, Provisions: -1, ProvisionsCapacity: 300, IsSupply: false,
            Training: training);
    }

    private static int DamageWith(int training)
    {
        var atk = Sword(1, 1, new HexCoord(0, 0), training: training);
        var target = Sword(2, 2, new HexCoord(1, 0), mode: UnitMode.March); // 반격·손실 배제
        return Orchestrator().Run(new[] { atk, target }).Combat!.DamageDealt[new UnitId(1)];
    }

    [Fact]
    public void 훈련도낮으면_공격이_약하다()
    {
        // 훈련 0(공 −10%) < 훈련 50(기준) < 훈련 100(+10%).
        Assert.True(DamageWith(0) < DamageWith(50));
        Assert.True(DamageWith(50) < DamageWith(100));
    }
}
