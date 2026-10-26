namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;

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
}
