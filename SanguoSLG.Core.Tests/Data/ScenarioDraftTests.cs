namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;

public sealed class ScenarioDraftTests
{
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
}
