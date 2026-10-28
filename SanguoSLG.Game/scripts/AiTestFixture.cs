using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

internal enum AiTestCase
{
    Balanced,
    PlayerHalf,
    EnemiesHalf,
}

internal sealed class AiTestFixture
{
    public HexMap Map { get; }
    public IReadOnlyList<Faction> Factions { get; }
    public IReadOnlyList<City> Cities { get; }
    public IReadOnlyList<GeneralPosting> Postings { get; }
    public IReadOnlyList<GarrisonForce> Garrisons { get; }
    public IReadOnlyList<PortShipStock> Ships { get; }
    public IReadOnlyList<RuinDefinition> Ruins { get; }
    public IReadOnlyList<CityIntel> Intel { get; }
    public bool FullVision { get; }

    private AiTestFixture(HexMap map, IReadOnlyList<Faction> factions, IReadOnlyList<City> cities,
        IReadOnlyList<GeneralPosting> postings, IReadOnlyList<GarrisonForce> garrisons,
        IReadOnlyList<PortShipStock> ships, IReadOnlyList<RuinDefinition> ruins,
        bool fullVision = true, IReadOnlyList<CityIntel>? intel = null)
    {
        Map = map;
        Factions = factions;
        Cities = cities;
        Postings = postings;
        Garrisons = garrisons;
        Ships = ships;
        Ruins = ruins;
        FullVision = fullVision;
        Intel = intel ?? (!fullVision ? [] : factions.SelectMany(faction => cities
            .Where(city => city.Owner != faction.Id)
            .Select(city => new CityIntel(faction.Id, city.Id)))
            .ToList());
    }

    public IReadOnlyList<GarrisonForce> GarrisonsFor(AiTestCase testCase, FactionId player)
    {
        if (testCase == AiTestCase.Balanced) return Garrisons;
        return Garrisons.Select(garrison =>
        {
            var isPlayer = Cities.Single(city => city.Id == garrison.City).Owner == player;
            var halve = testCase == AiTestCase.PlayerHalf ? isPlayer : !isPlayer;
            return halve ? garrison with { Troops = garrison.Troops / 2 } : garrison;
        }).ToList();
    }

    public static AiTestFixture Load()
    {
        var path = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"),
            "..", "data", "aitest-fixture.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var bounds = root.GetProperty("map");
        var terrain = new Dictionary<HexCoord, TerrainType>();
        foreach (var patch in root.GetProperty("terrain_rectangles").EnumerateArray())
        {
            var type = Enum.Parse<TerrainType>(patch.GetProperty("terrain").GetString()!, true);
            for (var q = Number(patch, "min_q"); q <= Number(patch, "max_q"); q++)
                for (var r = Number(patch, "min_r"); r <= Number(patch, "max_r"); r++)
                    terrain[new HexCoord(q, r)] = type;
        }

        var factions = root.GetProperty("factions").EnumerateArray()
            .Select(item => new Faction(new FactionId(Number(item, "id")),
                Text(item, "name"), new GeneralId(Number(item, "ruler")), 0, Text(item, "color")))
            .ToList();
        var cities = root.GetProperty("cities").EnumerateArray()
            .Select(item =>
            {
                var port = item.TryGetProperty("port", out var portValue)
                    ? Enum.Parse<PortSize>(portValue.GetString()!, true) : PortSize.None;
                var city = new City(new CityId(Number(item, "id")), Text(item, "name"),
                    new HexCoord(Number(item, "q"), Number(item, "r")),
                    new FactionId(Number(item, "faction")), Number(item, "provisions"),
                    Enum.Parse<CastleSize>(Text(item, "castle"), true),
                    Gold: Number(item, "gold"), Security: 80, Population: 100_000,
                    Ore: 10_000, Horses: 3_000, Paddies: 2, Farms: 2, Villages: 2,
                    Wall: Number(item, "wall"), Port: port);
                if (port != PortSize.None)
                    terrain[city.Position] = TerrainType.PortSmall;
                return city;
            }).ToList();
        var postings = root.GetProperty("postings").EnumerateArray()
            .Select(item => new GeneralPosting(new GeneralId(Number(item, "general")),
                new FactionId(Number(item, "faction")), new CityId(Number(item, "city"))))
            .ToList();
        var garrisons = root.GetProperty("garrisons").EnumerateArray()
            .Select(item => new GarrisonForce(new CityId(Number(item, "city")),
                Text(item, "troop"), Number(item, "count"), 60)).ToList();
        var ships = root.GetProperty("ships").EnumerateArray()
            .Select(item => new PortShipStock(new CityId(Number(item, "city")),
                Text(item, "code"), Number(item, "count"))).ToList();
        var ruins = root.GetProperty("ruins").EnumerateArray()
            .Select(item => new RuinDefinition(Text(item, "id"), Text(item, "name"),
                new HexCoord(Number(item, "q"), Number(item, "r")),
                Text(item, "troop_code"), Number(item, "max_defenders"))).ToList();
        var map = new HexMap(Number(bounds, "min_q"), Number(bounds, "max_q"),
            Number(bounds, "min_r"), Number(bounds, "max_r"), terrain);
        Validate(map, factions, cities, postings, garrisons, ships, ruins);
        return new AiTestFixture(map, factions, cities, postings, garrisons, ships, ruins);
    }

    public static AiTestFixture LoadNewMap()
    {
        var dataDirectory = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data"));
        var presetPath = ProjectSettings.GlobalizePath("user://map-presets/east-asia.json");
        using var currentMap = JsonDocument.Parse(File.ReadAllText(
            ProjectSettings.GlobalizePath("res://assets/maps/east-asia-map.json")));
        var preset = ScenarioMapPresetResizer.Resize(ScenarioMapPresetStore.Load(presetPath),
            currentMap.RootElement.GetProperty("width").GetInt32(),
            currentMap.RootElement.GetProperty("height").GetInt32());
        preset = AddNewMapTestPorts(preset);
        var scenario = ScenarioMapPresetApplier.Apply(
            new ScenarioLoader().LoadFromDirectory(dataDirectory), preset);
        var japaneseRegions = new HashSet<string>(StringComparer.Ordinal)
            { "owari", "mikawa", "kai", "echigo", "mutsu", "shinano", "aki", "satsuma" };
        var japanRuler = scenario.Generals.First(general => general.Name == "오다 노부나가").Id;
        var factions = scenario.Factions.Where(faction => faction.Id.Value is 1 or 2)
            .Select(faction => faction with { Gold = 20000 })
            .Append(new Faction(new FactionId(3), "일본", japanRuler, 20000, "#c18742"))
            .ToArray();
        // Fixed seed makes every new-map run reproducible while scattering both factions
        // across the edited map. City geometry, terrain and direction stay unchanged.
        var random = new Random(190);
        var shuffledIds = scenario.Cities.OrderBy(_ => random.Next())
            .Select(city => city.Id).ToArray();
        var owners = shuffledIds.Select((id, index) => (id, owner: new FactionId(index % 2 + 1)))
            .ToDictionary(item => item.id, item => item.owner);
        foreach (var site in preset.Sites.Where(site => site.Name is "후쿠오카" or "나가사키" or "일본항"))
            owners[new CityId(site.Id)] = new FactionId(3);
        foreach (var site in preset.Sites.Where(site => site.Name is "부산" or "한국항"))
            owners[new CityId(site.Id)] = new FactionId(1);
        var cities = scenario.Cities.Select(city => city with
        {
            Owner = owners[city.Id], Security = 85, Gold = 12000, Provisions = 48000,
        }).ToArray();
        // Concentrate officers at active border sites so the AI can actually form armies.
        var postings = new List<GeneralPosting>();
        foreach (var faction in factions)
        {
            var enemies = cities.Where(city => city.Owner != faction.Id).ToArray();
            var owned = cities.Where(city => city.Owner == faction.Id
                    && (faction.Id.Value == 3 ? city.Name == "일본항" : !city.IsPort))
                .OrderBy(city => enemies.Min(enemy => city.Position.Distance(enemy.Position)))
                .ThenBy(city => city.Id.Value).ToArray();
            if (owned.Length == 0) owned = cities.Where(city => city.Owner == faction.Id).ToArray();
            var generalIds = faction.Id.Value == 3
                ? scenario.Generals.Where(general => japaneseRegions.Contains(general.Region)
                    && general.Id != japanRuler).Select(general => general.Id)
                    .OrderBy(id => id.Value).Prepend(japanRuler)
                : scenario.Generals.Where(general => !japaneseRegions.Contains(general.Region)
                    && factions.All(owner => owner.Ruler != general.Id))
                    .Select(general => general.Id).OrderBy(id => id.Value)
                    .Where((_, index) => index % 2 == faction.Id.Value - 1)
                    .Prepend(faction.Ruler);
            var index = 0;
            foreach (var generalId in generalIds)
                postings.Add(new GeneralPosting(generalId, faction.Id,
                    owned[(index++ / 4) % owned.Length].Id));
        }
        var garrisons = cities.SelectMany(city =>
        {
            if (city.Name == "한국항")
                return new[] { new GarrisonForce(city.Id, "swordsman", 1000, 60) };
            var scale = 1 + city.Id.Value % 4;
            return new[]
            {
                new GarrisonForce(city.Id, "swordsman", 6000 * scale, 60),
                new GarrisonForce(city.Id, "archer", 2500 * scale, 60),
                new GarrisonForce(city.Id, "cavalry", 2500 * scale, 60),
                new GarrisonForce(city.Id, "thunder_cart", 1000 * scale, 60),
            };
        }).ToArray();
        var ships = cities.Where(city => city.IsPort)
            .Select(city => new PortShipStock(city.Id, "small_boat", 8)).ToArray();
        GD.Print($"[newmap] cells={scenario.Map.Count} cities={cities.Length} "
            + $"player={cities.Count(city => city.Owner.Value == 1)} "
            + $"enemy={cities.Count(city => city.Owner.Value == 2)} "
            + $"japan={cities.Count(city => city.Owner.Value == 3)} factions={factions.Length}");
        return new AiTestFixture(scenario.Map, factions, cities, postings, garrisons, ships, [],
            fullVision: false, intel: [new CityIntel(new FactionId(3),
                cities.First(city => city.Name == "한국항").Id)]);
    }

    private static ScenarioMapPreset AddNewMapTestPorts(ScenarioMapPreset preset)
    {
        var directions = new[] { new HexCoord(1, 0), new HexCoord(0, 1),
            new HexCoord(-1, 1), new HexCoord(-1, 0), new HexCoord(0, -1), new HexCoord(1, -1) };
        var sites = preset.Sites.ToList();
        foreach (var (cityName, portName) in new[] { ("부산", "한국항"), ("나가사키", "일본항") })
        {
            var city = sites.First(site => site.Name == cityName);
            var candidates = from row in Enumerable.Range(Math.Max(0, city.Row - 9),
                    Math.Min(preset.Height - 1, city.Row + 9) - Math.Max(0, city.Row - 9) + 1)
                from column in Enumerable.Range(Math.Max(0, city.Column - 9),
                    Math.Min(preset.Width - 1, city.Column + 9) - Math.Max(0, city.Column - 9) + 1)
                where "PFMDBSTNQ".Contains(preset.Rows[row][column])
                from direction in Enumerable.Range(0, 6)
                let water = preset.ToAxial(column, row) + directions[direction]
                where water.Q >= 0 && water.Q < preset.Width
                let waterRow = water.R + (water.Q - (water.Q & 1)) / 2
                where waterRow >= 0 && waterRow < preset.Height
                    && preset.Rows[waterRow][water.Q] is 'W' or 'O'
                orderby (column - city.Column) * (column - city.Column)
                    + (row - city.Row) * (row - city.Row), row, column
                select new MapEditorSite(sites.Max(site => site.Id) + 1, portName, "port",
                    "small", column, row, direction);
            MapEditorSite? selected = null;
            foreach (var candidate in candidates)
            {
                try
                {
                    new ScenarioMapPreset(preset.Version, preset.Width, preset.Height,
                        preset.Rows, sites.Append(candidate).ToArray()).Validate();
                    selected = candidate;
                    break;
                }
                catch (InvalidDataException) { }
            }
            if (selected is null) throw new InvalidDataException($"{portName} 배치 가능한 해안이 없습니다.");
            sites.Add(selected);
        }
        return preset with { Sites = sites };
    }

    private static int Number(JsonElement item, string name) => item.GetProperty(name).GetInt32();
    private static string Text(JsonElement item, string name) => item.GetProperty(name).GetString()!;

    private static void Validate(HexMap map, IReadOnlyList<Faction> factions, IReadOnlyList<City> cities,
        IReadOnlyList<GeneralPosting> postings, IReadOnlyList<GarrisonForce> garrisons,
        IReadOnlyList<PortShipStock> ships, IReadOnlyList<RuinDefinition> ruins)
    {
        if (factions.Count != 3 || factions.Select(f => f.Id).Distinct().Count() != 3
            || cities.Select(c => c.Id).Distinct().Count() != cities.Count
            || postings.Select(p => p.General).Distinct().Count() != postings.Count
            || cities.Any(c => !map.Contains(c.Position)
                || !factions.Any(f => f.Id == c.Owner))
            || postings.Any(p => !cities.Any(c => c.Id == p.Location && c.Owner == p.Faction))
            || garrisons.Any(g => !cities.Any(c => c.Id == g.City))
            || ships.Any(s => !cities.Any(c => c.Id == s.City && c.IsPort))
            || ruins.Count != 3 || ruins.Select(ruin => ruin.Id).Distinct().Count() != ruins.Count
            || ruins.Select(ruin => ruin.Position).Distinct().Count() != ruins.Count
            || ruins.Any(ruin => !map.Contains(ruin.Position)
                || !TerrainRules.CanEnter(MovementDomain.Land, map.TerrainAt(ruin.Position))
                || cities.Any(city => CastleFootprint.TilesFor(city).Contains(ruin.Position))
                || ruin.MaxDefenders != 15_000))
            throw new InvalidDataException("AI 검증장 배치의 세력·성·장수·병력·선박 참조가 잘못되었습니다.");
    }
}
