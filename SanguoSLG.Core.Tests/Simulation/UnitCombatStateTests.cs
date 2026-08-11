namespace SanguoSLG.Core.Tests.Simulation;

using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

/// <summary>부대 전투 지속 상태 — 선봉 우선 발동·하루 갱신·성복귀·계략 발동(design-skill/stratagem).</summary>
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
    public void 야전5일이면_두게이지_준비_선봉우선_발동후_부관차례()
    {
        var s = UnitCombatState.Create(80, A["peerless"], A["iron_wall"]).AdvanceField(5);
        Assert.True(s.VanguardGauge.IsReady && s.AdjutantGauge.IsReady);

        var (skill1, s2) = s.FiringActive();
        Assert.Equal("peerless", skill1!.Code);           // 선봉 우선
        Assert.False(s2.VanguardGauge.IsReady);            // 선봉 소비
        Assert.True(s2.AdjutantGauge.IsReady);             // 부관은 대기

        var (skill2, _) = s2.FiringActive();
        Assert.Equal("iron_wall", skill2!.Code);          // 다음 교전엔 부관
    }

    [Fact]
    public void 준비된_액티브없으면_null()
    {
        var (skill, _) = UnitCombatState.Create(80, A["peerless"]).AdvanceField(4).FiringActive();
        Assert.Null(skill);
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
