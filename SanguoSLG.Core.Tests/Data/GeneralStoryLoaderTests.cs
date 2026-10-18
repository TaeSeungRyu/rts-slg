namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using Xunit;

public sealed class GeneralStoryLoaderTests
{
    [Fact]
    public void 승전_패전_사건을_제외한_대화_사건을_읽는다()
    {
        var definitions = new GeneralStoryLoader().LoadFromDirectory(TestData.DataDirectory());
        Assert.True(definitions.Count >= 8);
        Assert.All(definitions, d => Assert.True(d.Variants.Count >= 1));
        Assert.DoesNotContain(definitions, d => d.Kind is "victory" or "wounded");
    }

    [Fact]
    public void 동맹_성공_실패와_군주_단독_참모_동석_대사는_각각_다섯_가지다()
    {
        var definitions = new GeneralStoryLoader().LoadFromDirectory(TestData.DataDirectory());
        foreach (var kind in new[] { "alliance_success", "alliance_fail",
            "alliance_success_advisor", "alliance_fail_advisor" })
        {
            var definition = Assert.Single(definitions, item => item.Kind == kind);
            Assert.Equal(5, definition.Variants.Count);
            Assert.All(definition.Variants, lines =>
            {
                Assert.Contains(lines, line => line.Contains("{ruler}"));
                if (kind.EndsWith("_advisor")) Assert.Contains(lines, line => line.Contains("{advisor}"));
            });
        }
    }

    [Fact]
    public void 성_일상_대사는_서로_다른_스무_가지다()
    {
        var definition = Assert.Single(new GeneralStoryLoader().LoadFromDirectory(TestData.DataDirectory()),
            item => item.Kind == "city_bark");
        Assert.Equal(20, definition.Variants.Count);
        Assert.Equal(20, definition.Variants.Select(lines => Assert.Single(lines)).Distinct().Count());
    }

    [Fact]
    public void 중복_사건코드는_거부한다()
    {
        var loader = new GeneralStoryLoader();
        var json = System.IO.File.ReadAllText(System.IO.Path.Combine(TestData.DataDirectory(), "general-story-events.json"));
        var bad = json.Replace("\"kind\":\"capture\"", "\"kind\":\"wall\"");
        Assert.Throws<System.IO.InvalidDataException>(() => loader.LoadFromJson(bad));
    }
}
