using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Domain;

public class CastleFootprintTests
{
    [Theory]
    [InlineData(CastleSize.Small, 1)]
    [InlineData(CastleSize.Medium, 3)]
    [InlineData(CastleSize.Large, 5)]
    public void 발자국_타일_수는_등급_정의와_같다(CastleSize size, int expected)
    {
        Assert.Equal(expected, CastleFootprint.OffsetsFor(size).Count);
    }

    [Theory]
    [InlineData(CastleSize.Medium)]
    [InlineData(CastleSize.Large)]
    public void 다중_타일_발자국은_서로_붙어있다(CastleSize size)
    {
        var offsets = CastleFootprint.OffsetsFor(size);
        Assert.All(offsets, tile =>
            Assert.Contains(offsets, other => other != tile && tile.Distance(other) == 1));
    }

    [Fact]
    public void 실제_시나리오에서_발자국은_맵_안_평야이며_서로_겹치지_않는다()
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());

        var seen = new HashSet<HexCoord>();
        foreach (var city in scenario.Cities)
        {
            foreach (var tile in CastleFootprint.TilesFor(city))
            {
                Assert.True(scenario.Map.Contains(tile), $"{city.Name}의 발자국 {tile}이 맵 밖이다.");
                Assert.Equal(TerrainType.Plains, scenario.Map.TerrainAt(tile));
                Assert.True(seen.Add(tile), $"{city.Name}의 발자국 {tile}이 다른 도시와 겹친다.");
            }
        }
    }
}
