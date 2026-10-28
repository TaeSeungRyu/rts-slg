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
        ['S'] = TerrainType.Rocks,
        ['T'] = TerrainType.RockHill,
        ['X'] = TerrainType.Karst,
        ['C'] = TerrainType.Cliff,
        ['I'] = TerrainType.IceMountain,
        ['E'] = TerrainType.IceWallLarge,
        ['L'] = TerrainType.IceWallSmall,
        ['N'] = TerrainType.Swamp,
        ['Q'] = TerrainType.DesertCactus,
        ['V'] = TerrainType.WaterRocks,
    };

    public static Scenario Apply(Scenario source, ScenarioMapPreset preset)
    {
        preset.Validate();
        if (!preset.Sites.Any(site => site.Kind is "city" or "port"))
            throw new InvalidDataException("게임 시작용 지도에는 성 또는 항구가 필요합니다.");
        var terrain = new Dictionary<HexCoord, TerrainType>();
        for (var row = 0; row < preset.Height; row++)
        for (var column = 0; column < preset.Width; column++)
            terrain[preset.ToAxial(column, row)] = Terrain[preset.Rows[row][column]];
        foreach (var facility in preset.Sites.Where(site => site.Kind == "facility"))
            terrain[preset.ToAxial(facility.Column, facility.Row)] = facility.Size switch
            {
                "paddy" => TerrainType.Paddy,
                "farm" => TerrainType.Farm,
                "village" => TerrainType.Village1,
                _ => TerrainType.Workshop,
            };
        var validTiles = terrain.Keys.ToHashSet();
        var minR = validTiles.Min(tile => tile.R);
        var maxR = validTiles.Max(tile => tile.R);
        var map = new HexMap(0, preset.Width - 1, minR, maxR, terrain, validTiles);

        var baselineByName = source.Cities.ToDictionary(city => city.Name, StringComparer.Ordinal);
        var baselineById = source.Cities.ToDictionary(city => city.Id.Value);
        var baseCity = source.Cities[0];
        var citySites = preset.Sites.Where(site => site.Kind is "city" or "port" or "gate").OrderBy(site => site.Id).ToArray();
        var cities = citySites.Select(site =>
        {
            var template = baselineByName.GetValueOrDefault(site.Name)
                ?? baselineById.GetValueOrDefault(site.Id)
                ?? baseCity;
            var isPort = site.Kind == "port";
            var isGate = site.Kind == "gate";
            var castle = isPort ? CastleSize.Small : site.Size switch
            {
                "large" => CastleSize.Large,
                "medium" => CastleSize.Medium,
                _ => CastleSize.Small,
            };
            return template with
            {
                Id = new CityId(site.Id),
                Name = site.Name,
                Position = preset.ToAxial(site.Column, site.Row),
                Castle = castle,
                Wall = isGate ? site.Size == "medium" ? 3_000 : 2_000
                    : CastleWall.Max(castle, source.Balance, template.WallLevel),
                Port = isPort ? site.Size == "medium" ? PortSize.Medium : PortSize.Small : PortSize.None,
                Gate = isGate ? site.Size == "medium" ? GateSize.Medium : GateSize.Small : null,
                Owner = isGate ? new FactionId(0) : template.Owner,
                Provisions = isGate ? 0 : template.Provisions,
                Gold = isGate ? 0 : template.Gold,
                Population = isGate ? 0 : template.Population,
                Governor = isGate ? null : template.Governor,
                Strategist = isGate ? null : template.Strategist,
                SecurityOfficer = isGate ? null : template.SecurityOfficer,
                DomesticOfficer = isGate ? null : template.DomesticOfficer,
                RecruitmentOfficer = isGate ? null : template.RecruitmentOfficer,
                TrainingOfficer = isGate ? null : template.TrainingOfficer,
                Direction = site.Direction,
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
        var newCitiesByName = cities.ToDictionary(city => city.Name, StringComparer.Ordinal);
        var facilities = preset.Sites.Where(site => site.Kind == "facility" &&
            site.ParentCityId is { } parent && knownCityIds.Contains(new CityId(parent)))
            .Select(site => new FacilityPlacement(new CityId(site.ParentCityId!.Value),
                preset.ToAxial(site.Column, site.Row), site.Size, FacilityHealth.Level1))
            .ToArray();
        return source with
        {
            Map = map,
            Cities = cities,
            Features = preset.Sites.Where(site => site.Kind == "landmark" && site.Size == "waterfall_cliff")
                .Select(site => new MapFeature(FeatureType.WaterfallCliff,
                    preset.ToAxial(site.Column, site.Row))).ToArray(),
            Conditions = new TileConditionMap(),
            Postings = source.PostingList.Select(posting =>
            {
                var oldCity = source.Cities.FirstOrDefault(city => city.Id == posting.Location);
                var preferred = oldCity is null ? null : newCitiesByName.GetValueOrDefault(oldCity.Name);
                var destination = preferred ?? cities.FirstOrDefault(city => city.Owner == posting.Faction)
                    ?? cities[0];
                return posting with { Location = destination.Id };
            }).ToArray(),
            Ruins = preset.Sites.Where(site => site.Kind == "ruin")
                .Select(site =>
                {
                    var kind = MapRuinCatalog.Find(site.Size)!;
                    return new RuinDefinition($"map_ruin_{site.Id}", site.Name,
                        preset.ToAxial(site.Column, site.Row), kind.TroopCode,
                        kind.Defenders, kind.Naval);
                }).ToArray(),
            InitialFacilities = facilities,
            MapPreset = preset.Snapshot(),
        };
    }
}
