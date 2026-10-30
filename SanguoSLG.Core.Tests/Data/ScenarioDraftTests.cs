namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class ScenarioDraftTests
{
    [Theory]
    [InlineData("city", "small", 600)]
    [InlineData("city", "medium", 1200)]
    [InlineData("city", "large", 2000)]
    [InlineData("port", "medium", 600)]
    [InlineData("gate", "small", 2000)]
    [InlineData("gate", "medium", 3000)]
    public void ScenarioWallFollowsSiteSize(string kind, string size, int expected)
    {
        var site = new MapEditorSite(1, "거점", kind, size, 1, 1);
        Assert.Equal(expected, ScenarioWallDefaults.ForSite(site,
            new SanguoSLG.Core.Simulation.BalanceConfig(MonthlyTaxPerCity: 100)));
    }

    private static readonly IReadOnlySet<int> Generals = new HashSet<int> { 1, 2 };
    private static ScenarioMapPreset Map() => new(1, 6, 5,
        Enumerable.Repeat("PPPPPP", 5).ToArray(),
        [new MapEditorSite(1, "첫 성", "city", "small", 1, 1),
            new MapEditorSite(2, "둘째 성", "city", "small", 4, 3)]);

    private static ScenarioDraft Draft() => new(1, "test", "시험 시나리오", "", 190, 1, 1,
        "test-map", ScenarioMapArchive.RevisionOf(Map()),
        [new ScenarioFactionEntry(1, "위", 1, "#3366AA")],
        [new ScenarioCityEntry(1, 1, 100, 100, 100, 80, [], []),
            new ScenarioCityEntry(2, 0, 0, 0, 100, 50, [], [])],
        [new ScenarioGeneralEntry(1, 1, 1)]);

    [Fact]
    public void ValidScenarioRoundTripsWithoutChangingMap()
    {
        var map = Map();
        Assert.Empty(Draft().Validate(map, Generals));
        var path = Path.Combine(Path.GetTempPath(), $"scenario-{Guid.NewGuid():N}.json");
        try
        {
            ScenarioDraftStore.Save(path, Draft(), map, Generals);
            var loaded = ScenarioDraftStore.Load(path);
            Assert.Equal("test", loaded.Id);
            Assert.Equal(2, loaded.Cities.Count);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void DeletedCityAndOfficerPostingCannotBeSaved()
    {
        var draft = Draft() with
        {
            Cities = [new ScenarioCityEntry(1, 1, 100, 100, 100, 80, [], [])],
            Generals = [new ScenarioGeneralEntry(1, 1, 2)],
        };
        var errors = draft.Validate(Map(), Generals);
        Assert.Contains(errors, error => error.Contains("거점 목록"));
        Assert.Contains(errors, error => error.Contains("삭제된 거점"));
    }

    [Fact]
    public void NonPortShipsAndUnknownFactionAreRejected()
    {
        var draft = Draft() with
        {
            Cities = [new ScenarioCityEntry(1, 9, 100, 100, 100, 80, [],
                    [new ScenarioShipEntry("ship_small", 1)]),
                new ScenarioCityEntry(2, 0, 0, 0, 100, 50, [], [])],
        };
        var errors = draft.Validate(Map(), Generals);
        Assert.Contains(errors, error => error.Contains("존재하지 않는 세력"));
        Assert.Contains(errors, error => error.Contains("선박"));
    }

    [Fact]
    public void RootBindingKeepsAssignmentsAndAddsNewNeutralSites()
    {
        var map = Map() with { Sites = [.. Map().Sites,
            new MapEditorSite(3, "새 성", "city", "small", 2, 3)] };
        var root = new ScenarioMapVersion("default-east-asia", "기본 지도",
            ScenarioMapArchive.RevisionOf(map), map);
        var updated = ScenarioRootMapBinding.Bind(Draft() with { MapId = "default-east-asia" }, root);
        Assert.Equal(root.Revision, updated.MapRevision);
        Assert.Equal(3, updated.Cities.Count);
        Assert.Equal(1, updated.Cities.Single(city => city.SiteId == 1).OwnerFactionId);
        Assert.Equal(0, updated.Cities.Single(city => city.SiteId == 3).OwnerFactionId);
        Assert.Empty(updated.Validate(map, Generals));
    }

    [Fact]
    public void RootBindingRejectsDeletedOccupiedSiteAndLeavesCustomMapAlone()
    {
        var map = Map() with { Sites = [Map().Sites[1]] };
        var root = new ScenarioMapVersion("default-east-asia", "기본 지도",
            ScenarioMapArchive.RevisionOf(map), map);
        Assert.Throws<InvalidDataException>(() => ScenarioRootMapBinding.Bind(
            Draft() with { MapId = "default-east-asia" }, root));
        var custom = Draft() with { MapId = "user-map" };
        Assert.Same(custom, ScenarioRootMapBinding.Bind(custom, root));
    }

    [Fact]
    public void RootBindingRelocatesDeletedCityAndItsOfficerToNearestFreeSite()
    {
        var old = Map();
        var map = old with { Sites = [old.Sites[1],
            new MapEditorSite(3, "대체 성", "city", "small", 2, 1)] };
        var root = new ScenarioMapVersion("default-east-asia", "기본 지도",
            ScenarioMapArchive.RevisionOf(map), map);
        var updated = ScenarioRootMapBinding.Bind(Draft() with { MapId = "default-east-asia" }, root, old);
        Assert.Equal(1, updated.Cities.Single(city => city.SiteId == 3).OwnerFactionId);
        Assert.Equal(3, updated.Generals.Single().SiteId);
        Assert.Empty(updated.Validate(map, Generals));
    }

    [Fact]
    public void HiddenFactionCannotStartAndAppearsAtNearestNeutralSiteInItsYear()
    {
        var hidden = new ScenarioFactionEntry(2, "후발 세력", 2, "#aabbcc", false, true, 191, 1);
        var draft = Draft() with
        {
            Factions = [.. Draft().Factions, hidden],
            Generals = [.. Draft().Generals, new ScenarioGeneralEntry(2, 2, null)],
        };
        Assert.Empty(draft.Validate(Map(), Generals));
        Assert.NotEmpty((draft with { Factions = [.. Draft().Factions, hidden with { Playable = true }] })
            .Validate(Map(), Generals));
        var state = new GameState(361, 190,
            [new Faction(new FactionId(1), "위", new GeneralId(1), 0, "#3366aa")],
            [new City(new CityId(1), "첫 성", new HexCoord(1, 1), new FactionId(1), 100),
                new City(new CityId(2), "둘째 성", new HexCoord(4, 3), new FactionId(0), 0)],
            [], HiddenFactionArrivals: [new HiddenFactionArrival(
                new Faction(new FactionId(2), "후발 세력", new GeneralId(2), 0, "#aabbcc"),
                191, new CityId(1), [new GeneralId(2)])]);
        var appeared = HiddenFactionArrivalService.Advance(state);
        Assert.Equal(2, appeared.Factions.Count);
        Assert.Equal(new FactionId(2), appeared.Cities.Single(city => city.Id.Value == 2).Owner);
        Assert.Equal(new CityId(2), appeared.Assignments.Single().Location);
        Assert.Equal(10000, appeared.Garrisons.Single().Troops);
        Assert.Empty(appeared.HiddenFactionArrivals!);
    }
}
