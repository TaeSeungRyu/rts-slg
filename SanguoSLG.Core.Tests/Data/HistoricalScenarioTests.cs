namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;

public sealed class HistoricalScenarioTests
{
    [Fact]
    public void SupportGeneralsKeepRecruitmentCandidatesInEveryScenario()
    {
        var root = Path.GetDirectoryName(TestData.DataDirectory())!;
        var source = new ScenarioLoader().LoadFromDirectory(TestData.DataDirectory());
        var support = source.Generals.Where(general => general.Id.Value is >= 306 and <= 544)
            .Select(general => general.Id.Value).ToHashSet();
        Assert.Equal(239, support.Count);
        foreach (var id in support)
            Assert.True(File.Exists(Path.Combine(root, "SanguoSLG.Game", "assets", "portraits", $"{id}.png")));

        var directory = Path.Combine(root, "SanguoSLG.Game", "assets", "scenarios");
        foreach (var path in Directory.GetFiles(directory, "*.json"))
        {
            var draft = ScenarioDraftStore.Load(path);
            var placed = draft.Generals.Select(general => general.GeneralId).ToArray();
            Assert.Equal(placed.Length, placed.Distinct().Count());
            Assert.InRange(placed.Count(support.Contains), 1, support.Count - 1);
        }
    }

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
