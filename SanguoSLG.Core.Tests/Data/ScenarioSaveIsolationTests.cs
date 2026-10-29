namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;

public sealed class ScenarioSaveIsolationTests
{
    [Fact]
    public void SaveKeepsItsOwnMapAfterUserMapChanges()
    {
        var map = new ScenarioMapPreset(1, 6, 5,
            Enumerable.Repeat("PPPPPP", 5).ToArray(),
            [new MapEditorSite(1, "성", "city", "small", 1, 1)]);
        var changed = map with { Rows = Enumerable.Repeat("PPPFPP", 5).ToArray() };
        var root = Path.Combine(Path.GetTempPath(), "scenario-save-" + Guid.NewGuid().ToString("N"));
        try
        {
            var saves = new SaveSlotStore(Path.Combine(root, "saves"));
            var state = new GameState(1, 190, [], [], [], MapPresetSnapshot: map.Snapshot());
            saves.Save(1, state);
            var originalRevision = ScenarioMapArchive.RevisionOf(map);
            Assert.NotEqual(originalRevision, ScenarioMapArchive.RevisionOf(changed));
            var loaded = saves.Load(1);
            Assert.NotNull(loaded.MapPresetSnapshot);
            Assert.Equal(originalRevision, ScenarioMapArchive.RevisionOf(loaded.MapPresetSnapshot));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
