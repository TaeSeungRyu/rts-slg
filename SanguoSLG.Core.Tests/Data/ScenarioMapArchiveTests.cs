namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;

public sealed class ScenarioMapArchiveTests
{
    private static ScenarioMapPreset Map(int cityId = 1) => new(1, 6, 5,
        Enumerable.Repeat("PPPPPP", 5).ToArray(),
        [new MapEditorSite(cityId, "성", "city", "small", 1, 1)]);

    [Fact]
    public void SavesImmutableVersionsAndDetectsScenarioReferences()
    {
        var root = Path.Combine(Path.GetTempPath(), $"map-archive-{Guid.NewGuid():N}");
        try
        {
            var archive = new ScenarioMapArchive(Path.Combine(root, "maps"),
                Path.Combine(root, "scenarios"));
            var first = archive.SaveVersion("test-map", "테스트", Map());
            var second = archive.SaveVersion("test-map", "테스트", Map(2));
            Assert.NotEqual(first.Revision, second.Revision);
            Assert.Equal(2, archive.ListVersions().Count);
            Assert.Equal(1, archive.Load("test-map", first.Revision).Preset.Sites.Single().Id);
            var scenarioPath = Path.Combine(root, "scenarios", "test.json");
            Directory.CreateDirectory(Path.GetDirectoryName(scenarioPath)!);
            File.WriteAllText(scenarioPath, System.Text.Json.JsonSerializer.Serialize(
                new ScenarioDraft(1, "test", "연동 시나리오", "", 190, 1, 1,
                    first.Id, first.Revision, [], [], [])));
            Assert.Equal(["연동 시나리오"], archive.ReferencingScenarios(first.Id, first.Revision));
            Assert.Empty(archive.ReferencingScenarios(second.Id, second.Revision));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void RejectsTamperedMapAndUnsafeIds()
    {
        var root = Path.Combine(Path.GetTempPath(), $"map-archive-{Guid.NewGuid():N}");
        try
        {
            var archive = new ScenarioMapArchive(Path.Combine(root, "maps"),
                Path.Combine(root, "scenarios"));
            Assert.Throws<InvalidDataException>(() => archive.SaveVersion("../escape", "지도", Map()));
            var version = archive.SaveVersion("safe", "지도", Map());
            var path = archive.PathFor(version.Id, version.Revision);
            File.WriteAllText(path, File.ReadAllText(path).Replace("PPPPPP", "FFFFFF"));
            Assert.Throws<InvalidDataException>(() => archive.Load(version.Id, version.Revision));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
