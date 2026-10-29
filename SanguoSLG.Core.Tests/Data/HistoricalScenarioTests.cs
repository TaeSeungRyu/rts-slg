namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;

public sealed class HistoricalScenarioTests
{
    [Fact]
    public void BundledScenariosMatchDefaultMapAndCanStart()
    {
        var root = Path.GetDirectoryName(TestData.DataDirectory())!;
        var map = ScenarioMapPresetStore.Load(Path.Combine(root, "SanguoSLG.Game", "assets", "maps", "east-asia-user-preset.json"));
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var known = source.Generals.Select(general => general.Id.Value).ToHashSet();
        var directory = Path.Combine(root, "SanguoSLG.Game", "assets", "scenarios");
        var paths = Directory.GetFiles(directory, "*.json");
        Assert.Equal(6, paths.Length);
        foreach (var path in paths)
        {
            var draft = ScenarioDraftStore.Load(path);
            Assert.Empty(draft.Validate(map, known));
            var applied = ScenarioDraftApplier.Apply(source, draft, map);
            Assert.Equal(draft.Factions.Count, applied.Factions.Count);
            Assert.Equal(draft.Generals.Count, applied.PostingList.Count);
            Assert.All(draft.Factions, faction => Assert.Contains(applied.Cities,
                city => city.Owner.Value == faction.Id));
        }
    }
}
