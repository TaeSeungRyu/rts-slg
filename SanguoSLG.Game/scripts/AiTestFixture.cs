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

    private AiTestFixture(HexMap map, IReadOnlyList<Faction> factions, IReadOnlyList<City> cities,
        IReadOnlyList<GeneralPosting> postings, IReadOnlyList<GarrisonForce> garrisons,
        IReadOnlyList<PortShipStock> ships, IReadOnlyList<RuinDefinition> ruins)
    {
        Map = map;
        Factions = factions;
        Cities = cities;
        Postings = postings;
        Garrisons = garrisons;
        Ships = ships;
        Ruins = ruins;
        Intel = factions.SelectMany(faction => cities
            .Where(city => city.Owner != faction.Id)
            .Select(city => new CityIntel(faction.Id, city.Id)))
            .ToList();
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
        var scenario = ScenarioMapPresetApplier.Apply(
            new ScenarioLoader().LoadFromDirectory(dataDirectory), preset);
        var factions = scenario.Factions.Where(faction => faction.Id.Value is 1 or 2).ToArray();
        var sites = preset.Sites.Where(site => site.Kind is "city" or "port").ToArray();
        var middle = sites.Select(site => site.Column).Order().ElementAt(sites.Length / 2);
        var cities = scenario.Cities.Select(city =>
        {
            var site = sites.Single(site => site.Id == city.Id.Value);
            var owner = new FactionId(site.Column <= middle ? 1 : 2);
            return city with { Owner = owner, Security = 80, Gold = 5000, Provisions = 12000 };
        }).ToArray();
        // Give each side its own capital and officers. The edited map's city,
        // port, terrain and direction remain exactly as saved in the preset.
        var postings = new List<GeneralPosting>();
        foreach (var faction in factions)
        {
            var owned = cities.Where(city => city.Owner == faction.Id && !city.IsPort)
                .OrderBy(city => city.Id.Value).ToArray();
            if (owned.Length == 0) owned = cities.Where(city => city.Owner == faction.Id).ToArray();
            var generalIds = faction.Id.Value == 1 ? Enumerable.Range(1, 10) : Enumerable.Range(11, 10);
            var index = 0;
            foreach (var generalId in generalIds)
                postings.Add(new GeneralPosting(new GeneralId(generalId), faction.Id,
                    owned[index++ % owned.Length].Id));
        }
        var garrisons = cities.SelectMany(city => new[]
        {
            new GarrisonForce(city.Id, "swordsman", 12000, 60),
            new GarrisonForce(city.Id, "archer", 4000, 60),
        }).ToArray();
        var ships = cities.Where(city => city.IsPort)
            .Select(city => new PortShipStock(city.Id, "small_boat", 2)).ToArray();
        GD.Print($"[newmap] cells={scenario.Map.Count} cities={cities.Length} "
            + $"player={cities.Count(city => city.Owner.Value == 1)} "
            + $"enemy={cities.Count(city => city.Owner.Value == 2)} factions={factions.Length}");
        return new AiTestFixture(scenario.Map, factions, cities, postings, garrisons, ships, []);
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
