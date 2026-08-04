using SanguoSLG.Core.Data;
using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Data;

public class TerrainLoadingTests
{
    [Fact]
    public void 지형_행을_좌표별_지형으로_매핑한다()
    {
        var scenario = new ScenarioLoader().LoadFromJson(
            factionsJson: "[]",
            citiesJson: "[]",
            generalsJson: "[]",
            balanceJson: """{ "monthly_tax_per_city": 100 }""",
            mapJson: """
            {
              "min_q": 0, "max_q": 2, "min_r": 0, "max_r": 3,
              "terrain": {
                "legend": {
                  "G": "plains", "F": "forest", "M": "mountain", "D": "desert",
                  "R": "river", "B": "bridge", "W": "water_shallow", "V": "water_deep",
                  "S": "rocks", "H": "rock_hill", "O": "water_rocks"
                },
                "rows": [ "GFM", "DRB", "WVG", "SHO" ]
              }
            }
            """);

        var map = scenario.Map;
        Assert.Equal(TerrainType.Plains, map.TerrainAt(new HexCoord(0, 0)));
        Assert.Equal(TerrainType.Forest, map.TerrainAt(new HexCoord(1, 0)));
        Assert.Equal(TerrainType.Mountain, map.TerrainAt(new HexCoord(2, 0)));
        Assert.Equal(TerrainType.Desert, map.TerrainAt(new HexCoord(0, 1)));
        Assert.Equal(TerrainType.River, map.TerrainAt(new HexCoord(1, 1)));
        Assert.Equal(TerrainType.Bridge, map.TerrainAt(new HexCoord(2, 1)));
        Assert.Equal(TerrainType.WaterShallow, map.TerrainAt(new HexCoord(0, 2)));
        Assert.Equal(TerrainType.WaterDeep, map.TerrainAt(new HexCoord(1, 2)));
        Assert.Equal(TerrainType.Rocks, map.TerrainAt(new HexCoord(0, 3)));
        Assert.Equal(TerrainType.RockHill, map.TerrainAt(new HexCoord(1, 3)));
        Assert.Equal(TerrainType.WaterRocks, map.TerrainAt(new HexCoord(2, 3)));
    }

    [Fact]
    public void 지형이_없으면_모두_평야다()
    {
        var scenario = new ScenarioLoader().LoadFromJson(
            factionsJson: "[]",
            citiesJson: "[]",
            generalsJson: "[]",
            balanceJson: """{ "monthly_tax_per_city": 100 }""",
            mapJson: """{ "min_q": 0, "max_q": 1, "min_r": 0, "max_r": 1 }""");

        Assert.Equal(TerrainType.Plains, scenario.Map.TerrainAt(new HexCoord(1, 1)));
    }

    [Fact]
    public void 실제_시나리오에서_도시는_평야에_있다()
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());

        Assert.All(scenario.Cities, c =>
            Assert.Equal(TerrainType.Plains, scenario.Map.TerrainAt(c.Position)));
    }
}
