namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

/// <summary>계략 2일 예약·발동/캔슬 판정(design-stratagem.md "시전·판정").</summary>
public class StratagemReservationTests
{
    private static readonly Stratagem FirePlot =
        new StratagemLoader().LoadFromDirectory(TestData.DataDirectory()).Single(s => s.Code == "fire_plot");

    private static StratagemReservation Reserve() => StratagemReservation.Reserve(FirePlot, new UnitId(7));

    [Fact]
    public void 예약직후_2일남고_대기()
    {
        var r = Reserve();
        Assert.Equal(2, r.DaysUntilFire);
        Assert.False(r.IsDue);
        Assert.Equal(StratagemFireOutcome.Pending, r.Evaluate(targetValid: true));
    }

    [Fact]
    public void 이틀경과하면_발동일()
    {
        var r = Reserve().Tick(1).Tick(1);
        Assert.True(r.IsDue);
    }

    [Fact]
    public void 긴진행으로_한번에_지나쳐도_발동일()
        => Assert.True(Reserve().Tick(4).IsDue);

    [Fact]
    public void 발동일_대상유효면_발동()
        => Assert.Equal(StratagemFireOutcome.Fired, Reserve().Tick(2).Evaluate(targetValid: true));

    [Fact]
    public void 발동일_대상소실이면_캔슬()
        => Assert.Equal(StratagemFireOutcome.Cancelled, Reserve().Tick(2).Evaluate(targetValid: false));

    [Fact]
    public void 대기중이면_대상무효여도_아직_Pending()
        => Assert.Equal(StratagemFireOutcome.Pending, Reserve().Tick(1).Evaluate(targetValid: false));
}
