using SanguoSLG.Core.Domain;
using Xunit;

namespace SanguoSLG.Core.Tests.Domain;

public class IdentifiersTests
{
    [Fact]
    public void 같은값의_ID는_동등하다()
    {
        Assert.Equal(new FactionId(1), new FactionId(1));
        Assert.NotEqual(new FactionId(1), new FactionId(2));
    }

    [Fact]
    public void ID는_읽기쉬운_문자열로_표현된다()
    {
        Assert.Equal("F7", new FactionId(7).ToString());
        Assert.Equal("C7", new CityId(7).ToString());
        Assert.Equal("G7", new GeneralId(7).ToString());
    }
}
