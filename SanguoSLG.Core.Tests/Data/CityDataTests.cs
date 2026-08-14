namespace SanguoSLG.Core.Tests.Data;

using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using Xunit;

/// <summary>data/cities.json 무결성 — 슬롯 제한·지역 코드(spec-city.md).</summary>
public class CityDataTests
{
    [Fact]
    public void 시설수는_성곽등급_슬롯을_넘지않는다()
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        foreach (var c in scenario.Cities)
        {
            var slots = c.Castle switch { CastleSize.Large => 9, CastleSize.Medium => 6, _ => 3 };
            Assert.True(c.Paddies + c.Farms + c.Villages <= slots,
                $"{c.Name}: 시설 {c.Paddies + c.Farms + c.Villages} > 슬롯 {slots}");
        }
    }

    [Fact]
    public void 도시_지역코드는_regions에_존재하고_인구는_최대치_이하다()
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var regions = new RegionLoader().LoadFromDirectory(TestData.DataDirectory())
            .Select(r => r.Code).ToHashSet();
        var b = scenario.Balance;

        foreach (var c in scenario.Cities)
        {
            Assert.Contains(c.Region, regions);
            var max = c.Castle switch
            {
                CastleSize.Large => b.PopulationMaxLarge,
                CastleSize.Medium => b.PopulationMaxMedium,
                _ => b.PopulationMaxSmall,
            };
            Assert.True(c.Population <= max, $"{c.Name}: 인구 {c.Population} > 최대 {max}");
        }
    }
}
