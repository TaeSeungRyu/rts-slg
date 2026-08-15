namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>부대 군량(design-unit-state 1단계) — 경과일 비례 소모, 고갈 시 이탈, 미추적은 무한.</summary>
public class ProvisionsTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static AdvanceOrchestrator Orchestrator()
    {
        var map = new HexMap(0, 30, -5, 5);
        var movement = new MovementSimulator(new PassabilityMap(map, [], []));
        // 소모 10/1만/일, 고갈 이탈 5%/일.
        return new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70),
            provisionsPer10kPerDay: 10, starvationLossPercentPerDay: 5);
    }

    private static CombatUnit March(int id, HexCoord pos, HexCoord target, int troops = 10000, int provisions = -1)
    {
        var field = new FieldUnit(new UnitId(id), new FactionId(1), pos, 2, 2, 1,
            MovementDomain.Land, UnitMode.March, target, id);
        var stats = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.River, troops);
        return new CombatUnit(field, stats, new TroopPool(troops, 0), UnitCombatState.Create(60),
            60, 60, troops, TroopClass.Infantry, provisions);
    }

    [Fact]
    public void 군량은_경과일과_병력에_비례해_소모된다()
    {
        // 1만 병력, 7일 행군(먼 목표) → 10×7 = 70 소모. 300 → 230.
        var u = March(1, new HexCoord(0, 0), new HexCoord(20, 0), troops: 10000, provisions: 300);
        var turn = Orchestrator().Run(new[] { u });

        Assert.Equal(7, turn.Movement.Days);
        Assert.Equal(230, turn.Units.Single().Provisions);
        Assert.Empty(turn.Starvation);
    }

    [Fact]
    public void 군량_고갈이면_병력이_이탈한다()
    {
        // 군량 30, 7일 소모 70 → 고갈. 이탈 5%×7 = 35% → 1만 → 6500, 군량 0.
        var u = March(1, new HexCoord(0, 0), new HexCoord(20, 0), troops: 10000, provisions: 30);
        var turn = Orchestrator().Run(new[] { u });

        var after = turn.Units.Single();
        Assert.Equal(0, after.Provisions);
        Assert.Equal(6500, after.Pool.Active);
        Assert.Equal(0, after.Pool.Wounded);        // 이탈은 부상 없이 소실
        Assert.Equal(3500, turn.Starvation[new UnitId(1)]);
    }

    [Fact]
    public void 미추적_부대는_군량을_소모하지_않는다()
    {
        var u = March(1, new HexCoord(0, 0), new HexCoord(20, 0), provisions: -1);
        var turn = Orchestrator().Run(new[] { u });

        Assert.Equal(-1, turn.Units.Single().Provisions);
        Assert.Empty(turn.Starvation);
    }

    [Fact]
    public void 소모는_병력에_비례한다_절반병력_절반소모()
    {
        // 5000 병력, 7일 → 10×7×5000/10000 = 35 소모. 300 → 265.
        var u = March(1, new HexCoord(0, 0), new HexCoord(20, 0), troops: 5000, provisions: 300);
        var turn = Orchestrator().Run(new[] { u });
        Assert.Equal(265, turn.Units.Single().Provisions);
    }
}
