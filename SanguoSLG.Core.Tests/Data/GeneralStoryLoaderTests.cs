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
        Assert.All(definitions, d => Assert.True(d.Variants.Count >= 3));
        Assert.DoesNotContain(definitions, d => d.Kind is "victory" or "wounded");
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
