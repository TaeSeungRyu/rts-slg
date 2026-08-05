using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Spatial;

public class FeatureFootprintTests
{
    [Fact]
    public void 중간산_발자국은_붙어있는_2타일이다()
    {
        var offsets = FeatureFootprint.OffsetsFor(FeatureType.MountainMedium);

        Assert.Equal(2, offsets.Count);
        Assert.Equal(1, offsets[0].Distance(offsets[1]));
    }

    [Fact]
    public void 큰산_발자국은_서로_붙은_3타일_삼각이다()
    {
        var offsets = FeatureFootprint.OffsetsFor(FeatureType.MountainLarge);

        Assert.Equal(3, offsets.Count);
        Assert.All(offsets, tile =>
            Assert.All(offsets, other => Assert.True(tile == other || tile.Distance(other) == 1)));
    }

    [Fact]
    public void 실제_시나리오에서_지물은_맵_안_평야이며_성곽과_겹치지_않는다()
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());

        Assert.NotEmpty(scenario.Features);

        var occupied = new HashSet<HexCoord>();
        foreach (var city in scenario.Cities)
        {
            foreach (var tile in CastleFootprint.TilesFor(city))
            {
                occupied.Add(tile);
            }
        }

        foreach (var feature in scenario.Features)
        {
            foreach (var tile in FeatureFootprint.TilesFor(feature))
            {
                Assert.True(scenario.Map.Contains(tile), $"{feature.Type}의 발자국 {tile}이 맵 밖이다.");
                Assert.Equal(TerrainType.Plains, scenario.Map.TerrainAt(tile));
                Assert.True(occupied.Add(tile), $"{feature.Type}의 발자국 {tile}이 다른 점유와 겹친다.");
            }
        }
    }
}
