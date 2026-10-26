namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class ScenarioMapPresetTests
{
    [Fact]
    public void OffsetAxialRoundTripAndSnapshotAreStable()
    {
        var preset = new ScenarioMapPreset(1, 4, 3, ["PPPP", "PWWP", "PPPP"],
            [new MapEditorSite(1, "시험성", "city", "small", 3, 2)]);
        preset.Validate();
        for (var column = 0; column < preset.Width; column++)
        for (var row = 0; row < preset.Height; row++)
            Assert.Equal((column, row), preset.ToOffset(preset.ToAxial(column, row)));
        Assert.NotSame(preset.Rows, preset.Snapshot().Rows);
    }

    [Fact]
    public void StoreRoundTripsWithoutChangingOriginalAndRejectsOverlaps()
    {
        var path = Path.Combine(Path.GetTempPath(), "map-preset-" + Guid.NewGuid() + ".json");
        try
        {
            var preset = new ScenarioMapPreset(1, 2, 2, ["PP", "PW"],
                [new MapEditorSite(1, "성", "city", "small", 0, 0)]);
            ScenarioMapPresetStore.Save(path, preset);
            Assert.Equal(preset, ScenarioMapPresetStore.Load(path) with { Rows = preset.Rows, Sites = preset.Sites });
            Assert.Throws<InvalidDataException>(() => (preset with
            {
                Sites = [..preset.Sites, new MapEditorSite(2, "중복", "port", "small", 0, 0)]
            }).Validate());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void AppliedMapIsIsolatedAndSavedCampaignKeepsItsSnapshot()
    {
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var baselineCount = source.Map.Count;
        var preset = new ScenarioMapPreset(1, 4, 3,
            ["PPPP", "PPWP", "PPPP"],
            [new MapEditorSite(1, "새 성", "city", "small", 0, 0),
                new MapEditorSite(100, "새 항", "port", "small", 1, 0, 1),
                new MapEditorSite(101, "새 논", "facility", "paddy", 0, 1, 0, 1)]);
        var applied = ScenarioMapPresetApplier.Apply(source, preset);
        Assert.Equal(baselineCount, source.Map.Count);
        Assert.Equal(12, applied.Map.Count);
        Assert.False(applied.Map.Contains(new HexCoord(3, -2)));
        Assert.Equal(2, applied.Cities.Count);
        var state = GameState.FromScenario(applied);
        Assert.Equal("새 성", state.Cities[0].Name);
        Assert.Single(state.Placements);
        var reloaded = SaveService.Deserialize(SaveService.Serialize(state));
        Assert.Equal(preset.Rows, reloaded.MapPresetSnapshot!.Rows);
        Assert.Equal(preset.Sites, reloaded.MapPresetSnapshot.Sites);
        Assert.NotEqual("새 성", source.Cities[0].Name);
    }
}
