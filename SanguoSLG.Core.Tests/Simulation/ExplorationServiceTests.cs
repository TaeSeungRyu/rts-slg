namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class ExplorationServiceTests
{
    private sealed class FixedRandom(int value) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => value;
    }

    [Theory]
    [InlineData(0, ExplorationResultKind.DivineBeast)]
    [InlineData(1, ExplorationResultKind.AncientRelic)]
    [InlineData(2, ExplorationResultKind.LocalClan)]
    [InlineData(11, ExplorationResultKind.LocalClan)]
    [InlineData(12, ExplorationResultKind.Rumor)]
    [InlineData(16, ExplorationResultKind.Rumor)]
    [InlineData(17, ExplorationResultKind.None)]
    [InlineData(99, ExplorationResultKind.None)]
    public void 탐색_확률_구간을_결정론적으로_판정한다(int roll, ExplorationResultKind expected)
    {
        Assert.Equal(expected, ExplorationService.KindForRoll(roll));
    }

    [Fact]
    public void 지방호족은_금과_군량_보상을_가진다()
    {
        var state = new GameState(3, 190, [], [], []);
        var city = new City(new CityId(1), "장안", new HexCoord(0, 0), new FactionId(1), 1000);
        var general = new General(new GeneralId(1), "조조", new Dictionary<TroopClass, AptitudeGrade>(), 70, 90, 90);

        var discovery = new ExplorationService().Explore(state, city, general, new FixedRandom(2));

        Assert.Equal(ExplorationResultKind.LocalClan, discovery.Kind);
        Assert.Equal(200, discovery.Gold);
        Assert.Equal(600, discovery.Provisions);
        Assert.Equal(new GeneralId(1), discovery.Explorer);
    }
}
