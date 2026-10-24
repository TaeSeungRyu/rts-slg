namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;

public sealed class AiSupportPriorityTests
{
    [Fact]
    public void 지원중인_공세_판단은_도래할_때마다_칠일씩_미룬다()
    {
        var faction = new Faction(new FactionId(1), "지원 세력", new GeneralId(1),
            1000, "red")
        {
            AiNextWarDecisionDay = 30,
        };
        Assert.Equal(30, AiWarDecisionClock.DelayForSupport(faction, 29));
        Assert.Equal(37, AiWarDecisionClock.DelayForSupport(faction, 30));
        Assert.Equal(44, AiWarDecisionClock.DelayForSupport(
            faction with { AiNextWarDecisionDay = 37 }, 37));
    }
}
