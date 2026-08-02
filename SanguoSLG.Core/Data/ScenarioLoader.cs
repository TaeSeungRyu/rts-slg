namespace SanguoSLG.Core.Data;

using System.Text.Json;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

/// <summary>
/// data/*.json을 읽어 Scenario로 매핑한다. 엔진에 의존하지 않으며 System.IO만 사용한다.
/// </summary>
public sealed class ScenarioLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>지정한 디렉토리에서 factions/cities/generals/balance.json을 읽는다.</summary>
    public Scenario LoadFromDirectory(string dataDirectory)
    {
        string Read(string fileName) => File.ReadAllText(Path.Combine(dataDirectory, fileName));
        return LoadFromJson(
            Read("factions.json"),
            Read("cities.json"),
            Read("generals.json"),
            Read("balance.json"),
            Read("map.json"));
    }

    /// <summary>JSON 문자열에서 직접 로드한다(테스트·임베딩용).</summary>
    public Scenario LoadFromJson(string factionsJson, string citiesJson, string generalsJson, string balanceJson, string mapJson)
    {
        var factions = Deserialize<List<FactionDto>>(factionsJson, "factions")
            .Select(d => new Faction(new FactionId(d.Id), d.Name, new GeneralId(d.Ruler), d.Gold))
            .ToList();

        var cities = Deserialize<List<CityDto>>(citiesJson, "cities")
            .Select(d => new City(new CityId(d.Id), d.Name, new HexCoord(d.Q, d.R), new FactionId(d.Owner), d.Provisions))
            .ToList();

        var generals = Deserialize<List<GeneralDto>>(generalsJson, "generals")
            .Select(d => new General(new GeneralId(d.Id), d.Name, d.Leadership, d.Might, d.Intellect, d.Politics, d.Charisma))
            .ToList();

        var balanceDto = Deserialize<BalanceDto>(balanceJson, "balance");
        var balance = new BalanceConfig(balanceDto.MonthlyTaxPerCity);

        var mapDto = Deserialize<MapDto>(mapJson, "map");
        var map = new HexMap(mapDto.MinQ, mapDto.MaxQ, mapDto.MinR, mapDto.MaxR);

        return new Scenario(factions, cities, generals, balance, map);
    }

    private static T Deserialize<T>(string json, string what)
    {
        var result = JsonSerializer.Deserialize<T>(json, Options);
        if (result is null)
        {
            throw new InvalidDataException($"{what} 데이터를 역직렬화할 수 없습니다.");
        }

        return result;
    }
}
