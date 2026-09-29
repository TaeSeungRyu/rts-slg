namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed class RecruitmentPointBankTests
{
    [Fact]
    public void 동일한_세력과_활동키는_한번만_지급된다()
    {
        var faction = new FactionId(1);
        var state = new GameState(1, 190, [], [], []);

        state = RecruitmentPointBank.Grant(state, faction, 100, "ruin:r1", out var first);
        state = RecruitmentPointBank.Grant(state, faction, 100, "ruin:r1", out var repeated);

        Assert.True(first);
        Assert.False(repeated);
        Assert.Equal(100, RecruitmentPointBank.Balance(state, faction));
        Assert.Single(state.RecruitmentPointHistory);
    }

    [Fact]
    public void 같은_유적도_다른_세력은_각각_최초등록_보상을_받는다()
    {
        var state = new GameState(1, 190, [], [], []);
        state = RecruitmentPointBank.Grant(state, new FactionId(1), 100, "ruin:r1", out _);
        state = RecruitmentPointBank.Grant(state, new FactionId(2), 100, "ruin:r1", out _);

        Assert.Equal(100, RecruitmentPointBank.Balance(state, new FactionId(1)));
        Assert.Equal(100, RecruitmentPointBank.Balance(state, new FactionId(2)));
        Assert.Equal(2, state.RecruitmentPointHistory.Count);
    }
}
