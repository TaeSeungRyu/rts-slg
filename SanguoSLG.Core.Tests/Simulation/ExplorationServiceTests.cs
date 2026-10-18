namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class ExplorationServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void 임시_신수확률은_50퍼센트이며_다른보상을_유지한다(int politics)
    {
        var normal = Enumerable.Range(0, 100).Select(r => ExplorationService.KindForRoll(r, politics)).ToList();
        var boosted = Enumerable.Range(0, 100).Select(r => ExplorationService.KindForRoll(r, politics, 50)).ToList();
        Assert.Equal(1, normal.Count(k => k == ExplorationResultKind.DivineBeast));
        Assert.Equal(50, boosted.Count(k => k == ExplorationResultKind.DivineBeast));
        foreach (var kind in new[] { ExplorationResultKind.AncientRelic, ExplorationResultKind.LocalClan, ExplorationResultKind.Rumor })
            Assert.Equal(normal.Count(k => k == kind), boosted.Count(k => k == kind));
    }

    private sealed class FixedRandom(int value) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => value;
    }

    private sealed class BeastRandom(int beastIndex) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive)
            => maxExclusive == 3 ? beastIndex : minInclusive;
    }

    [Fact]
    public void 고대유물_당첨은_정의된_단서_종류를_선택한다()
    {
        var relics = new SanguoSLG.Core.Data.AncientRelicLoader().LoadFromDirectory(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../data")));
        var city = new City(new(1), "장안", default, new(1), 1000);
        var general = new General(new(1), "탐색장", new Dictionary<TroopClass, AptitudeGrade>(), 70, 70, 70);
        var discovery = new ExplorationService(relics).Explore(
            new GameState(10, 190, [], [city], [general]), city, general, new FixedRandom(1));

        Assert.Equal(ExplorationResultKind.AncientRelic, discovery.Kind);
        Assert.Equal(relics[1].ClueCode, discovery.ClueCode);
        Assert.Equal(10, relics.Count);
    }

    [Theory]
    [InlineData(0, "divine_beast_trace")]
    [InlineData(1, "dragon_trace")]
    [InlineData(2, "phoenix_trace")]
    public void 신수_발견은_종류별_단서를_만들되_발견_확률은_그대로다(int beastIndex, string code)
    {
        var city = new City(new(1), "장안", default, new(1), 1000);
        var general = new General(new(1), "탐색장", new Dictionary<TroopClass, AptitudeGrade>(), 70, 70, 70);
        var discovery = new ExplorationService().Explore(new GameState(10, 190, [], [city], [general]),
            city, general, new BeastRandom(beastIndex));

        Assert.Equal(ExplorationResultKind.DivineBeast, discovery.Kind);
        Assert.Equal(code, discovery.Code);
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

    [Theory]
    [InlineData(11, 0, ExplorationResultKind.LocalClan)]
    [InlineData(12, 0, ExplorationResultKind.Rumor)]
    [InlineData(21, 100, ExplorationResultKind.LocalClan)]
    [InlineData(22, 100, ExplorationResultKind.Rumor)]
    [InlineData(26, 100, ExplorationResultKind.Rumor)]
    [InlineData(27, 100, ExplorationResultKind.None)]
    public void 정치가_높으면_지방호족_확률만_증가한다(int roll, int politics, ExplorationResultKind expected)
    {
        Assert.Equal(expected, ExplorationService.KindForRoll(roll, politics));
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

    [Fact]
    public void 탐색은_내정레벨의_유효정치를_반영한다()
    {
        var state = new GameState(3, 190, [], [], []);
        var city = new City(new CityId(1), "장안", new HexCoord(0, 0), new FactionId(1), 1000);
        var general = new General(new GeneralId(1), "탐색장", new Dictionary<TroopClass, AptitudeGrade>(),
            70, 70, 49, AdminLevel: 4);

        var discovery = new ExplorationService().Explore(state, city, general, new FixedRandom(16));

        Assert.Equal(ExplorationResultKind.LocalClan, discovery.Kind);
    }
}
