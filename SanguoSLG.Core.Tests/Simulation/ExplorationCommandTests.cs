namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class ExplorationCommandTests
{
    private sealed class FixedRandom(int value) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => value;
    }

    private static City City(int gold = 100, int provisions = 200) =>
        new(new CityId(1), "허창", new HexCoord(0, 0), new FactionId(1), provisions,
            Gold: gold);

    private static General General() => new(
        new GeneralId(1), "탐색관", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 50, Intellect: 50, Politics: 80);

    [Fact]
    public void 탐색_완료시_지방호족_보상을_도시에_정산하고_이력을_남긴다()
    {
        var service = new CommandService(new CommandBalance());
        var state = new GameState(1, 1, new List<Faction>(), [City()], [General()]);
        var issued = service.Issue(state, new CommandRequest(new CityId(1), CommandKind.Explore, new GeneralId(1))).State;

        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0), new CommandBalance(),
            random: new FixedRandom(2));
        var after = world.AdvanceDays(issued, 7);

        var city = after.Cities.Single();
        Assert.Equal(300, city.Gold);
        Assert.Equal(800, city.Provisions);
        var discovery = Assert.Single(after.Discoveries);
        Assert.Equal(ExplorationResultKind.LocalClan, discovery.Kind);
        Assert.Contains(world.LastEvents, e => e.Kind == WorldEventKind.Explore && e.Code == "local_clan_support");
    }

    [Fact]
    public void 탐색_완료시_성과없음도_이력과_보고사건을_남긴다()
    {
        var service = new CommandService(new CommandBalance());
        var state = new GameState(1, 1, new List<Faction>(), [City()], [General()]);
        var issued = service.Issue(state, new CommandRequest(new CityId(1), CommandKind.Explore, new GeneralId(1))).State;

        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0), new CommandBalance(),
            random: new FixedRandom(99));
        var after = world.AdvanceDays(issued, 7);

        Assert.Equal(100, after.Cities.Single().Gold);
        Assert.Equal(200, after.Cities.Single().Provisions);
        Assert.Equal(ExplorationResultKind.None, Assert.Single(after.Discoveries).Kind);
        Assert.Contains(world.LastEvents, e => e.Kind == WorldEventKind.Explore && e.Code == "nothing");
    }
}
