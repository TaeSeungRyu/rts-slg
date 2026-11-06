using SanguoSLG.Core.Data;

namespace SanguoSLG.Core.Tests.Data;

public sealed class ScenarioDraftCatalogTests
{
    [Fact]
    public void UserDraftOverridesBundledDraftByScenarioIdEvenWhenFileNamesDiffer()
    {
        var root = Path.Combine(Path.GetTempPath(), "scenario-catalog-" + Guid.NewGuid().ToString("N"));
        var builtIn = Path.Combine(root, "built-in");
        var user = Path.Combine(root, "user");
        Directory.CreateDirectory(builtIn);
        Directory.CreateDirectory(user);
        try
        {
            File.WriteAllText(Path.Combine(builtIn, "historical-184.json"), Json("historical-184", "원본"));
            File.WriteAllText(Path.Combine(user, "my-edited-scenario.json"), Json("historical-184", "수정본"));

            var result = ScenarioDraftCatalog.LoadEffective(builtIn, user);

            var scenario = Assert.Single(result);
            Assert.True(scenario.IsUser);
            Assert.Equal("수정본", scenario.Draft.Title);
            Assert.Equal("my-edited-scenario.json", Path.GetFileName(scenario.Path));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void UserCreatedScenariosStayAfterAllBuiltInsRegardlessOfStartYear()
    {
        var root = Path.Combine(Path.GetTempPath(), "scenario-order-" + Guid.NewGuid().ToString("N"));
        var builtIn = Path.Combine(root, "built-in");
        var user = Path.Combine(root, "user");
        Directory.CreateDirectory(builtIn);
        Directory.CreateDirectory(user);
        try
        {
            File.WriteAllText(Path.Combine(builtIn, "historical-184.json"), Json("historical-184", "기본 184"));
            File.WriteAllText(Path.Combine(builtIn, "fictional-revival.json"), Json("fictional-revival", "가상"));
            File.WriteAllText(Path.Combine(user, "historical-184.json"), Json("historical-184", "수정한 기본"));
            File.WriteAllText(Path.Combine(user, "scenario-early.json"), Json("scenario-early", "100년 제작"));
            File.WriteAllText(Path.Combine(user, "scenario-late.json"), Json("scenario-late", "300년 제작"));

            var result = ScenarioDraftCatalog.LoadEffective(builtIn, user);

            Assert.Equal(["historical-184", "fictional-revival", "scenario-early", "scenario-late"],
                result.Select(source => source.Draft.Id));
            Assert.Equal("수정한 기본", result[0].Draft.Title);
            Assert.True(result[0].IsUser);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string Json(string id, string title) => $$"""
    {"Version":1,"Id":"{{id}}","Title":"{{title}}","Description":"설명","StartYear":184,
    "StartMonth":3,"StartDay":1,"MapId":"map","MapRevision":"revision",
    "Factions":[],"Cities":[],"Generals":[]}
    """;
}
