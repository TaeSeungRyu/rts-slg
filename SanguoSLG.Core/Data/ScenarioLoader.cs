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
            .Select(d => new City(
                new CityId(d.Id), d.Name, new HexCoord(d.Q, d.R), new FactionId(d.Owner), d.Provisions,
                ParseCastle(d.Castle)))
            .ToList();

        var generals = Deserialize<List<GeneralDto>>(generalsJson, "generals")
            .Select(d => new General(new GeneralId(d.Id), d.Name, d.Leadership, d.Might, d.Intellect, d.Politics, d.Charisma))
            .ToList();

        var balanceDto = Deserialize<BalanceDto>(balanceJson, "balance");
        var balance = new BalanceConfig(balanceDto.MonthlyTaxPerCity);

        var mapDto = Deserialize<MapDto>(mapJson, "map");
        var map = BuildMap(mapDto);
        var features = mapDto.Features
            .Select(d => new MapFeature(ParseFeature(d.Type), new HexCoord(d.Q, d.R)))
            .ToList();

        return new Scenario(factions, cities, generals, balance, map, features);
    }

    private static HexMap BuildMap(MapDto dto)
    {
        IReadOnlyDictionary<HexCoord, TerrainType>? terrain = null;
        if (dto.Terrain is { Rows.Count: > 0 })
        {
            var legend = dto.Terrain.Legend.ToDictionary(kv => kv.Key[0], kv => ParseTerrain(kv.Value));
            var grid = new Dictionary<HexCoord, TerrainType>();
            for (var i = 0; i < dto.Terrain.Rows.Count; i++)
            {
                var row = dto.Terrain.Rows[i];
                var r = dto.MinR + i;
                for (var j = 0; j < row.Length; j++)
                {
                    if (legend.TryGetValue(row[j], out var type))
                    {
                        grid[new HexCoord(dto.MinQ + j, r)] = type;
                    }
                }
            }

            terrain = grid;
        }

        return new HexMap(dto.MinQ, dto.MaxQ, dto.MinR, dto.MaxR, terrain);
    }

    private static CastleSize ParseCastle(string name) => name switch
    {
        "small" => CastleSize.Small,
        "medium" => CastleSize.Medium,
        "large" => CastleSize.Large,
        _ => throw new InvalidDataException($"알 수 없는 성곽 등급: {name}"),
    };

    private static FeatureType ParseFeature(string name) => name switch
    {
        "mountain_medium" => FeatureType.MountainMedium,
        "mountain_large" => FeatureType.MountainLarge,
        "mountain_huge" => FeatureType.MountainHuge,
        "waterfall_cliff" => FeatureType.WaterfallCliff,
        _ => throw new InvalidDataException($"알 수 없는 지물: {name}"),
    };

    private static TerrainType ParseTerrain(string name) => name switch
    {
        "plains" => TerrainType.Plains,
        "forest" => TerrainType.Forest,
        "mountain" => TerrainType.Mountain,
        "desert" => TerrainType.Desert,
        "river" => TerrainType.River,
        "bridge" => TerrainType.Bridge,
        "water_shallow" => TerrainType.WaterShallow,
        "water_deep" => TerrainType.WaterDeep,
        "rocks" => TerrainType.Rocks,
        "rock_hill" => TerrainType.RockHill,
        "water_rocks" => TerrainType.WaterRocks,
        "paddy" => TerrainType.Paddy,
        "farm" => TerrainType.Farm,
        "workshop" => TerrainType.Workshop,
        "rock_mountain" => TerrainType.RockMountain,
        "karst" => TerrainType.Karst,
        "cliff" => TerrainType.Cliff,
        "ice_mountain" => TerrainType.IceMountain,
        "ice_wall_large" => TerrainType.IceWallLarge,
        "ice_wall_small" => TerrainType.IceWallSmall,
        "village_1" => TerrainType.Village1,
        "swamp" => TerrainType.Swamp,
        "desert_cactus" => TerrainType.DesertCactus,
        _ => throw new InvalidDataException($"알 수 없는 지형: {name}"),
    };

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
