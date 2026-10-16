namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using Xunit;

public sealed class GeneralStoryLoaderTests
{
    [Fact]
    public void 열가지_대화_사건과_세가지_이상_변형을_읽는다()
    {
        var definitions = new GeneralStoryLoader().LoadFromDirectory(TestData.DataDirectory());
        Assert.True(definitions.Count >= 10);
        Assert.All(definitions, d => Assert.True(d.Variants.Count >= 3));
        Assert.Equal(8, definitions.Single(d => d.Kind == "victory").VoiceLines!.Count);
    }

    [Fact]
    public void 중복_사건코드는_거부한다()
    {
        var loader = new GeneralStoryLoader();
        var json = System.IO.File.ReadAllText(System.IO.Path.Combine(TestData.DataDirectory(), "general-story-events.json"));
        var bad = json.Replace("\"kind\":\"wounded\"", "\"kind\":\"victory\"");
        Assert.Throws<System.IO.InvalidDataException>(() => loader.LoadFromJson(bad));
    }
}
