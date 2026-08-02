using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Data;

public class ScenarioLoaderTests
{
    // 테스트 바이너리 위치에서 위로 올라가며 실제 data 디렉토리를 찾는다.
    private static string FindDataDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "factions.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("data 디렉토리를 찾지 못했습니다.");
    }

    [Fact]
    public void LoadFromDirectory_실제_더미시나리오가_유효하다()
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(FindDataDirectory());

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
    }

    [Fact]
    public void LoadFromJson_snake_case_키를_도메인으로_매핑한다()
    {
        var scenario = new ScenarioLoader().LoadFromJson(
            factionsJson: """[ { "id": 1, "name": "위", "ruler": 5, "gold": 1000 } ]""",
            citiesJson: """[ { "id": 2, "name": "허창", "q": 3, "r": -1, "owner": 1, "provisions": 5000 } ]""",
            generalsJson: """[ { "id": 5, "name": "조조", "leadership": 96, "might": 72, "intellect": 91, "politics": 94, "charisma": 96 } ]""",
            balanceJson: """{ "monthly_tax_per_city": 120 }""");

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
    }
}
