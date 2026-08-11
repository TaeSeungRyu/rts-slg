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
