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

    private sealed class FixedRandomSequence(params int[] values) : IRandomSource
    {
        private int _index;
        public int Next(int minInclusive, int maxExclusive) => values[_index++ % values.Length];
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
        Assert.Equal(819, city.Provisions);
        var discovery = Assert.Single(after.Discoveries);
        Assert.Equal(ExplorationResultKind.LocalClan, discovery.Kind);
        Assert.Contains(world.LastEvents, e => e.Kind == WorldEventKind.Explore
            && e.Code == "local_clan_support" && e.Amount == 200 && e.ExtraAmount == 600);
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
        Assert.Equal(219, after.Cities.Single().Provisions);
        Assert.Equal(ExplorationResultKind.None, Assert.Single(after.Discoveries).Kind);
        Assert.Contains(world.LastEvents, e => e.Kind == WorldEventKind.Explore && e.Code == "nothing");
    }

    [Fact]
    public void 여러_성의_탐색_명령은_각_도시별로_독립_정산된다()
    {
        var service = new CommandService(new CommandBalance());
        var c1 = City();
        var c2 = City() with { Id = new CityId(2), Name = "업", Position = new HexCoord(5, 0), Gold = 300 };
        var g1 = General();
        var g2 = General() with { Id = new GeneralId(2), Name = "탐색관2" };
        var state = new GameState(1, 1, new List<Faction>(), [c1, c2], [g1, g2],
            Postings: [new GeneralPosting(g1.Id, c1.Owner, c1.Id), new GeneralPosting(g2.Id, c2.Owner, c2.Id)]);
        var first = service.Issue(state, new CommandRequest(c1.Id, CommandKind.Explore, g1.Id)).State;
        var second = service.Issue(first, new CommandRequest(c2.Id, CommandKind.Explore, g2.Id)).State;

        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0), new CommandBalance(),
            random: new FixedRandomSequence(2, 99));
        var after = world.AdvanceDays(second, 7);

        Assert.Equal(2, after.Discoveries.Count);
        Assert.Contains(after.Discoveries, d => d.City == c1.Id && d.Kind == ExplorationResultKind.LocalClan);
        Assert.Contains(after.Discoveries, d => d.City == c2.Id && d.Kind == ExplorationResultKind.None);
        Assert.Equal(300, after.Cities.Single(c => c.Id == c1.Id).Gold);
        Assert.Equal(300, after.Cities.Single(c => c.Id == c2.Id).Gold);
    }
}
