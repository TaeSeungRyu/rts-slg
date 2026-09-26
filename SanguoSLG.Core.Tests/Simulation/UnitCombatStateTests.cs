namespace SanguoSLG.Core.Tests.Simulation;

using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

/// <summary>부대 전투 지속 상태 — 선봉·부관 교대 발동·하루 갱신·성복귀·계략 발동.</summary>
public class UnitCombatStateTests
{
    private static readonly System.Collections.Generic.IReadOnlyDictionary<string, ActiveSkill> A =
        new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly Stratagem FirePlot =
        new StratagemLoader().LoadFromDirectory(TestData.DataDirectory()).Single(s => s.Code == "fire_plot");

    [Fact]
    public void 출전_게이지0_모략력가득_예약없음()
    {
        var s = UnitCombatState.Create(intellect: 80, vanguardActive: A["peerless"]);
        Assert.False(s.VanguardGauge.IsReady);
        Assert.Equal(80, s.Resource.Current);
        Assert.Null(s.Reservation);
    }

    [Fact]
    public void 공용게이지_6칸이면_선봉발동후_초기화되고_다음은_부관차례()
    {
        var s = UnitCombatState.Create(80, A["peerless"], A["iron_wall"]).AdvanceField(6);
        Assert.True(s.VanguardGauge.IsReady && s.AdjutantGauge.IsReady);

        var (skill1, s2) = s.FiringActive();
        Assert.Equal("peerless", skill1!.Code);
        Assert.Equal(0, s2.SharedActiveGauge.ElapsedDays);
        Assert.Equal(ActiveCommanderSlot.Adjutant, s2.ScheduledActiveSlot);

        var (tooEarly, charging) = s2.AdvanceField(5).FiringActive();
        Assert.Null(tooEarly);
        var (skill2, s3) = charging.AdvanceField(1).FiringActive();
        Assert.Equal("iron_wall", skill2!.Code);
        Assert.Equal(ActiveCommanderSlot.Vanguard, s3.ScheduledActiveSlot);
    }

    [Fact]
    public void 준비된_액티브없으면_null()
    {
        var (skill, _) = UnitCombatState.Create(80, A["peerless"]).AdvanceField(4).FiringActive();
        Assert.Null(skill);
    }

    [Fact]
    public void 효과미정_책략형은_발동하거나_게이지를_소비하지않는다()
    {
        var s = UnitCombatState.Create(80, A["fire_plot"]).AdvanceField(6);

        var (skill, after) = s.FiringActive();

        Assert.Null(skill);
        Assert.True(after.VanguardGauge.IsReady);
    }

    [Fact]
    public void 다음차례가_책략형이면_부관_일반액티브를_건너뛰지않는다()
    {
        var s = UnitCombatState.Create(80, A["fire_plot"], A["iron_wall"]).AdvanceField(6);

        var (skill, after) = s.FiringActive();

        Assert.Null(skill);
        Assert.True(after.SharedActiveGauge.IsReady);
        Assert.Equal(ActiveCommanderSlot.Vanguard, after.ScheduledActiveSlot);
    }

    [Fact]
    public void 책략형은_전용발동에서_오일게이지를_소비한다()
    {
        var s = UnitCombatState.Create(80, A["fire_plot"]).AdvanceField(6);

        var (skill, after) = s.FiringTactic();

        Assert.Equal("fire_plot", skill?.Code);
        Assert.False(after.VanguardGauge.IsReady);
    }

    [Fact]
    public void 액티브가_한개면_매주기_같은장수가_반복발동한다()
    {
        var firstReady = UnitCombatState.Create(80, A["peerless"]).AdvanceField(6);
        var (first, afterFirst) = firstReady.FiringActive();
        var (second, afterSecond) = afterFirst.AdvanceField(6).FiringActive();

        Assert.Equal("peerless", first?.Code);
        Assert.Equal("peerless", second?.Code);
        Assert.Equal(ActiveCommanderSlot.Vanguard, afterSecond.ScheduledActiveSlot);
    }

    [Fact]
    public void 구버전_서로다른게이지는_진행도가높은슬롯을_다음차례로복구한다()
    {
        var legacy = UnitCombatState.Create(80, A["peerless"], A["iron_wall"]) with
        {
            VanguardGauge = new ActiveGauge(0),
            AdjutantGauge = new ActiveGauge(6),
        };

        Assert.Equal(ActiveCommanderSlot.Adjutant, legacy.ScheduledActiveSlot);
        var (skill, after) = legacy.FiringDefenseActive();
        Assert.Equal("iron_wall", skill?.Code);
        Assert.Equal(0, after.SharedActiveGauge.ElapsedDays);
        Assert.Equal(ActiveCommanderSlot.Vanguard, after.ScheduledActiveSlot);
    }

    [Fact]
    public void 성복귀하면_게이지0_모략력충전_예약취소()
    {
        var s = UnitCombatState.Create(80, A["peerless"])
            .AdvanceField(6)
            .ReserveStratagem(FirePlot, new UnitId(9));
        s = s with { Resource = s.Resource.Spend(30) };

        var back = s.ReturnToCastle();
        Assert.False(back.VanguardGauge.IsReady);
        Assert.Equal(80, back.Resource.Current);
        Assert.Null(back.Reservation);
    }

    [Fact]
    public void 계략_예약2일뒤_대상유효면_발동_모략력소비_숙달증가()
    {
        var s = UnitCombatState.Create(80, masteryPoints: 20)     // Lv7
            .ReserveStratagem(FirePlot, new UnitId(9))
            .AdvanceField(2);

        Assert.Equal(StratagemFireOutcome.Fired, s.StratagemDue(targetValid: true));

        var (strat, after) = s.FireStratagem();
        Assert.Equal("fire_plot", strat.Code);
        Assert.Equal(80 - 15, after.Resource.Current);   // 화계 소모 15
        Assert.Equal(21, after.MasteryPoints);           // +1
        Assert.Null(after.Reservation);
    }

    [Fact]
    public void 계략_발동일_대상소실이면_캔슬_페널티없음()
    {
        var s = UnitCombatState.Create(80).ReserveStratagem(FirePlot, new UnitId(9)).AdvanceField(2);
        Assert.Equal(StratagemFireOutcome.Cancelled, s.StratagemDue(targetValid: false));

        var after = s.CancelStratagem();
        Assert.Equal(80, after.Resource.Current); // 모략력 그대로
        Assert.Null(after.Reservation);
    }

    [Fact]
    public void 숙달포인트로_레벨판정()
        => Assert.Equal(9, UnitCombatState.Create(80, masteryPoints: 150).MasteryLevel);
}
