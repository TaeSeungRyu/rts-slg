namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;

public class AiWarDecisionClockTests
{
    [Fact]
    public void 첫_판단은_즉시하고_다음_판단은_30_60일_후다()
    {
        var faction = new Faction(new FactionId(3), "촉", new GeneralId(1), 0, "red");
        Assert.True(AiWarDecisionClock.IsDue(faction, 1));
        var next = AiWarDecisionClock.NextDay(faction, 1);
        Assert.InRange(next, 31, 61);
        var stored = faction with { AiNextWarDecisionDay = next };
        Assert.False(AiWarDecisionClock.IsDue(stored, next - 1));
        Assert.True(AiWarDecisionClock.IsDue(stored, next));
        Assert.Equal(next, AiWarDecisionClock.NextDay(faction, 1));
    }
}
