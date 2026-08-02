using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Data;

public class ScenarioLoaderTests
{
    [Fact]
    public void LoadFromDirectory_실제_더미시나리오가_유효하다()
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());

        Assert.NotEmpty(scenario.Factions);
        Assert.NotEmpty(scenario.Cities);
        Assert.NotEmpty(scenario.Generals);
        Assert.True(scenario.Balance.MonthlyTaxPerCity > 0);

        // 참조 무결성: 모든 도시의 소유 세력이 실제 존재한다.
        var factionIds = scenario.Factions.Select(f => f.Id).ToHashSet();
        Assert.All(scenario.Cities, c => Assert.Contains(c.Owner, factionIds));

        // 참조 무결성: 모든 세력의 군주가 실제 무장으로 존재한다.
        var generalIds = scenario.Generals.Select(g => g.Id).ToHashSet();
        Assert.All(scenario.Factions, f => Assert.Contains(f.Ruler, generalIds));

        // 참조 무결성: 모든 도시가 맵 경계 안에 있다.
        Assert.All(scenario.Cities, c => Assert.True(scenario.Map.Contains(c.Position), $"{c.Name}이 맵 밖에 있다."));
    }

    [Fact]
    public void LoadFromJson_snake_case_키를_도메인으로_매핑한다()
    {
        var scenario = new ScenarioLoader().LoadFromJson(
            factionsJson: """[ { "id": 1, "name": "위", "ruler": 5, "gold": 1000 } ]""",
            citiesJson: """[ { "id": 2, "name": "허창", "q": 3, "r": -1, "owner": 1, "provisions": 5000 } ]""",
            generalsJson: """[ { "id": 5, "name": "조조", "leadership": 96, "might": 72, "intellect": 91, "politics": 94, "charisma": 96 } ]""",
            balanceJson: """{ "monthly_tax_per_city": 120 }""",
            mapJson: """{ "min_q": 0, "max_q": 5, "min_r": -1, "max_r": 2 }""");

        var faction = Assert.Single(scenario.Factions);
        Assert.Equal(new FactionId(1), faction.Id);
        Assert.Equal("위", faction.Name);
        Assert.Equal(new GeneralId(5), faction.Ruler);
        Assert.Equal(1000, faction.Gold);

        var city = Assert.Single(scenario.Cities);
        Assert.Equal(new CityId(2), city.Id);
        Assert.Equal(new HexCoord(3, -1), city.Position);
        Assert.Equal(new FactionId(1), city.Owner);
        Assert.Equal(5000, city.Provisions);

        var general = Assert.Single(scenario.Generals);
        Assert.Equal("조조", general.Name);
        Assert.Equal(96, general.Leadership);
        Assert.Equal(72, general.Might);

        Assert.Equal(120, scenario.Balance.MonthlyTaxPerCity);

        Assert.Equal(0, scenario.Map.MinQ);
        Assert.Equal(5, scenario.Map.MaxQ);
        Assert.Equal(-1, scenario.Map.MinR);
        Assert.Equal(2, scenario.Map.MaxR);
    }
}
