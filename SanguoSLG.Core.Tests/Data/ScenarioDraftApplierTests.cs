namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;

public sealed class ScenarioDraftApplierTests
{
    [Fact]
    public void SeededScenarioCanBeSavedLoadedAndApplied()
    {
        var root = FindRepositoryRoot();
        var source = new ScenarioLoader().LoadFromDirectory(Path.Combine(root, "data"));
        var preset = ScenarioMapPresetStore.Load(Path.Combine(root, "SanguoSLG.Game", "assets", "maps",
            "east-asia-user-preset.json"));
        var working = Path.Combine(Path.GetTempPath(), "scenario-apply-" + Guid.NewGuid().ToString("N"));
        try
        {
            var archive = new ScenarioMapArchive(Path.Combine(working, "maps"),
                Path.Combine(working, "scenarios"));
            var version = archive.SaveVersion("default-east-asia", "기본 동아시아", preset);
            var draft = ScenarioDraftApplier.CreateSeed("scenario-test", "시험", source, version);
            var known = source.Generals.Select(general => general.Id.Value).ToHashSet();
            var path = Path.Combine(working, "scenarios", "scenario-test.json");
            ScenarioDraftStore.Save(path, draft, preset, known);
            var applied = ScenarioDraftApplier.Apply(source, ScenarioDraftStore.Load(path),
                archive.Load(version.Id, version.Revision).Preset);
            Assert.Equal(preset.Sites.Count(site => site.Kind is "city" or "port" or "gate"), applied.Cities.Count);
            Assert.Equal(source.Factions.Count, applied.Factions.Count);
            Assert.All(applied.PostingList, posting => Assert.Contains(applied.Cities,
                city => city.Id == posting.Location));
        }
        finally { if (Directory.Exists(working)) Directory.Delete(working, true); }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SanguoSLG.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }
}
