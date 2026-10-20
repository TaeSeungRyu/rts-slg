using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
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

    private AiTestFixture(HexMap map, IReadOnlyList<Faction> factions, IReadOnlyList<City> cities,
        IReadOnlyList<GeneralPosting> postings, IReadOnlyList<GarrisonForce> garrisons,
        IReadOnlyList<PortShipStock> ships)
    {
        Map = map;
        Factions = factions;
        Cities = cities;
        Postings = postings;
        Garrisons = garrisons;
        Ships = ships;
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
        var map = new HexMap(Number(bounds, "min_q"), Number(bounds, "max_q"),
            Number(bounds, "min_r"), Number(bounds, "max_r"), terrain);
        Validate(map, factions, cities, postings, garrisons, ships);
        return new AiTestFixture(map, factions, cities, postings, garrisons, ships);
    }

    private static int Number(JsonElement item, string name) => item.GetProperty(name).GetInt32();
    private static string Text(JsonElement item, string name) => item.GetProperty(name).GetString()!;

    private static void Validate(HexMap map, IReadOnlyList<Faction> factions, IReadOnlyList<City> cities,
        IReadOnlyList<GeneralPosting> postings, IReadOnlyList<GarrisonForce> garrisons,
        IReadOnlyList<PortShipStock> ships)
    {
        if (factions.Count != 3 || factions.Select(f => f.Id).Distinct().Count() != 3
            || cities.Select(c => c.Id).Distinct().Count() != cities.Count
            || postings.Select(p => p.General).Distinct().Count() != postings.Count
            || cities.Any(c => !map.Contains(c.Position)
                || !factions.Any(f => f.Id == c.Owner))
            || postings.Any(p => !cities.Any(c => c.Id == p.Location && c.Owner == p.Faction))
            || garrisons.Any(g => !cities.Any(c => c.Id == g.City))
            || ships.Any(s => !cities.Any(c => c.Id == s.City && c.IsPort)))
            throw new InvalidDataException("AI 검증장 배치의 세력·성·장수·병력·선박 참조가 잘못되었습니다.");
    }
}
