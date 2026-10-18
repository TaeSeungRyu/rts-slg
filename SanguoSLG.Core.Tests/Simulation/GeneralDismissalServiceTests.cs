namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class GeneralDismissalServiceTests
{
    private static readonly GeneralId Officer = new(2);
    private static readonly CityId Home = new(1);

    private sealed class FixedRandom(int value) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => Math.Clamp(value, minInclusive, maxExclusive - 1);
    }

    private static GameState State() => new(1, 190,
        [new Faction(new FactionId(1), "위", new GeneralId(1), 0, "red"),
         new Faction(new FactionId(2), "촉", new GeneralId(3), 0, "blue")],
        [new City(Home, "업", new HexCoord(0, 0), new FactionId(1), 0,
            Governor: Officer, SecurityOfficer: Officer),
         new City(new CityId(2), "한중", new HexCoord(3, 0), new FactionId(2), 0)],
        [], Postings: [new GeneralPosting(new GeneralId(1), new FactionId(1), Home),
            new GeneralPosting(Officer, new FactionId(1), Home)],
        HeroUnlockStates: [new HeroUnlockState(Officer, HeroUnlockStatus.Recruited, new FactionId(1))]);

    [Theory]
    [InlineData(14)]
    [InlineData(35)]
    public void 해고_대기기간_뒤에만_다른_세력으로_합류한다(int days)
    {
        var service = new GeneralDismissalService(new FixedRandom(days));
        var dismissed = service.Dismiss(State(), Home, Officer);
        Assert.Null(dismissed.PostingOf(Officer));
        Assert.Null(dismissed.Cities[0].Governor);
        Assert.Null(dismissed.Cities[0].SecurityOfficer);
        Assert.Equal(1 + days, Assert.Single(dismissed.Dismissed).ReturnDay);
        dismissed = SaveService.Deserialize(SaveService.Serialize(dismissed));
        Assert.Equal(1 + days, Assert.Single(dismissed.Dismissed).ReturnDay);

        var before = service.Advance(dismissed with { Day = days });
        Assert.Null(before.PostingOf(Officer));
        var after = service.Advance(before with { Day = days + 1 });
        Assert.Equal(new FactionId(2), after.PostingOf(Officer)?.Faction);
        Assert.Equal(new CityId(2), after.PostingOf(Officer)?.Location);
        Assert.Empty(after.Dismissed);
        Assert.Equal(new FactionId(2), Assert.Single(after.HeroStates).EligibleFaction);
    }

    [Fact]
    public void 군주와_다른_도시의_장수는_해고할_수_없다()
    {
        var service = new GeneralDismissalService(new FixedRandom(14));
        Assert.Throws<InvalidOperationException>(() => service.Dismiss(State(), Home, new GeneralId(1)));
        Assert.Throws<InvalidOperationException>(() => service.Dismiss(State(), new CityId(2), Officer));
    }

    [Fact]
    public void 진행중인_업무가_있으면_해고할_수_없다()
    {
        var state = State() with { PendingCommands = [new CityCommand(Home, CommandKind.Explore,
            Officer, null, 1, 8, 0)] };
        Assert.Throws<InvalidOperationException>(() =>
            new GeneralDismissalService(new FixedRandom(14)).Dismiss(state, Home, Officer));
    }
}
