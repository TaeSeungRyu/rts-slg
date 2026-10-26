namespace SanguoSLG.Core.Data;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public static class ScenarioMapPresetApplier
{
    private static readonly IReadOnlyDictionary<char, TerrainType> Terrain = new Dictionary<char, TerrainType>
    {
        ['P'] = TerrainType.Plains,
        ['F'] = TerrainType.Forest,
        ['M'] = TerrainType.Mountain,
        ['H'] = TerrainType.RockMountain,
        ['D'] = TerrainType.Desert,
        ['R'] = TerrainType.River,
        ['B'] = TerrainType.Bridge,
        ['W'] = TerrainType.WaterShallow,
        ['O'] = TerrainType.WaterDeep,
    };

    public static Scenario Apply(Scenario source, ScenarioMapPreset preset)
    {
        preset.Validate();
        var terrain = new Dictionary<HexCoord, TerrainType>();
        for (var row = 0; row < preset.Height; row++)
        for (var column = 0; column < preset.Width; column++)
            terrain[preset.ToAxial(column, row)] = Terrain[preset.Rows[row][column]];
        var validTiles = terrain.Keys.ToHashSet();
        var minR = validTiles.Min(tile => tile.R);
        var maxR = validTiles.Max(tile => tile.R);
        var map = new HexMap(0, preset.Width - 1, minR, maxR, terrain, validTiles);

        var baselineByName = source.Cities.ToDictionary(city => city.Name, StringComparer.Ordinal);
        var baselineById = source.Cities.ToDictionary(city => city.Id.Value);
        var baseCity = source.Cities[0];
        var citySites = preset.Sites.Where(site => site.Kind is "city" or "port").OrderBy(site => site.Id).ToArray();
        var cities = citySites.Select(site =>
        {
            var template = baselineByName.GetValueOrDefault(site.Name)
                ?? baselineById.GetValueOrDefault(site.Id)
                ?? baseCity;
            var isPort = site.Kind == "port";
            return template with
            {
                Id = new CityId(site.Id),
                Name = site.Name,
                Position = preset.ToAxial(site.Column, site.Row),
                Castle = isPort ? CastleSize.Small : site.Size switch
                {
                    "large" => CastleSize.Large,
                    "medium" => CastleSize.Medium,
                    _ => CastleSize.Small,
                },
                Port = isPort ? site.Size == "medium" ? PortSize.Medium : PortSize.Small : PortSize.None,
                Paddies = preset.Sites.Count(facility => facility.Kind == "facility" &&
                    facility.ParentCityId == site.Id && facility.Size == "paddy"),
                Farms = preset.Sites.Count(facility => facility.Kind == "facility" &&
                    facility.ParentCityId == site.Id && facility.Size == "farm"),
                Villages = preset.Sites.Count(facility => facility.Kind == "facility" &&
                    facility.ParentCityId == site.Id && facility.Size == "village"),
                Workshop = preset.Sites.Any(facility => facility.Kind == "facility" &&
                    facility.ParentCityId == site.Id && facility.Size == "workshop"),
            };
        }).ToArray();
        var knownCityIds = cities.Select(city => city.Id).ToHashSet();
        var facilities = preset.Sites.Where(site => site.Kind == "facility" &&
            site.ParentCityId is { } parent && knownCityIds.Contains(new CityId(parent)))
            .Select(site => new FacilityPlacement(new CityId(site.ParentCityId!.Value),
                preset.ToAxial(site.Column, site.Row), site.Size, FacilityHealth.Level1))
            .ToArray();
        return source with
        {
            Map = map,
            Cities = cities,
            Features = [],
            Conditions = new TileConditionMap(),
            Postings = source.PostingList.Where(posting => posting.Location is { } location
                && knownCityIds.Contains(location)).ToArray(),
            Ruins = [],
            InitialFacilities = facilities,
            MapPreset = preset.Snapshot(),
        };
    }
}
