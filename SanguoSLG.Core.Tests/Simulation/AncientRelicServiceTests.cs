namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using Xunit;

public class AncientRelicServiceTests
{
    private static readonly IReadOnlyList<AncientRelicDefinition> Definitions =
        new AncientRelicLoader().LoadFromDirectory(Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../data")));

    private static ExplorationDiscovery Clue(int day, string code, int faction = 1)
        => new(day, new(faction), new(1), new(1), ExplorationResultKind.AncientRelic,
            "ancient_relic_clue", ClueCode: code);

    [Fact]
    public void 같은_종류_두개는_유물로_교환되고_재처리해도_중복_지급되지_않는다()
    {
        var code = Definitions[0].ClueCode;
        var state = new GameState(10, 190, [], [], [], ExplorationDiscoveries:
            [Clue(1, code), Clue(2, code), Clue(3, code)]);
        var service = new AncientRelicService(Definitions);

        var result = service.Resolve(state);
        Assert.Single(result.Treasures);
        Assert.Equal(Definitions[0].Code, result.Treasures[0].Code);
        Assert.Equal(2, result.Discoveries.Count(x => x.RelicTreasureId.Length > 0));
        Assert.Single(result.Discoveries, x => x.RelicTreasureId.Length == 0);
        Assert.Equal(result.Treasures, service.Resolve(result).Treasures);
    }

    [Fact]
    public void 다른_종류나_다른_세력의_단서는_합치지_않는다()
    {
        var state = new GameState(10, 190, [], [], [], ExplorationDiscoveries:
            [Clue(1, Definitions[0].ClueCode), Clue(2, Definitions[1].ClueCode),
             Clue(3, Definitions[0].ClueCode, 2)]);
        Assert.Empty(new AncientRelicService(Definitions).Resolve(state).Treasures);
    }

    [Fact]
    public void 구형_종류없는_단서는_보존하고_자동_소비하지_않는다()
    {
        var old = Clue(1, "") with { Text = "고대유물의 단서를 발견했습니다." };
        var state = new GameState(10, 190, [], [], [], ExplorationDiscoveries: [old, old]);
        var result = new AncientRelicService(Definitions).Resolve(state);
        Assert.Empty(result.Treasures);
        Assert.All(result.Discoveries, x => Assert.Equal("", x.ClueCode));
    }

    [Fact]
    public void 유물과_단서_소비기록은_저장_불러오기에서도_유지된다()
    {
        var state = new GameState(10, 190, [], [], [], ExplorationDiscoveries:
            [Clue(1, Definitions[0].ClueCode), Clue(2, Definitions[0].ClueCode)]);
        var awarded = new AncientRelicService(Definitions).Resolve(state);
        var loaded = SaveService.Deserialize(SaveService.Serialize(awarded));

        Assert.Single(loaded.Treasures);
        Assert.Equal(2, loaded.Discoveries.Count(x => x.RelicTreasureId == loaded.Treasures[0].Id));
        Assert.Single(new AncientRelicService(Definitions).Resolve(loaded).Treasures);
    }

    [Fact]
    public void 구형_미분류_단서_두개를_선택한_유물로_한번만_교환한다()
    {
        var old = Clue(1, "");
        var state = new GameState(10, 190, [], [], [], ExplorationDiscoveries: [old, old]);
        var service = new AncientRelicService(Definitions);

        var result = service.ConvertLegacy(state, new(1), Definitions[2].Code);

        Assert.True(result.Ok);
        Assert.Equal(Definitions[2].Code, result.State.Treasures.Single().Code);
        Assert.False(service.ConvertLegacy(result.State, new(1), Definitions[2].Code).Ok);
    }
}
