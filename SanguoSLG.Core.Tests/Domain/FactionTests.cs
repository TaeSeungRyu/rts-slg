using SanguoSLG.Core.Domain;
using Xunit;

namespace SanguoSLG.Core.Tests.Domain;

public class FactionTests
{
    [Fact]
    public void AddGold_자금을_더한_새_세력을_반환하고_원본은_불변이다()
    {
        var original = new Faction(new FactionId(1), "위", new GeneralId(1), Gold: 100, Color: "#2d5fd0");

        var richer = original.AddGold(50);

        Assert.Equal(150, richer.Gold);
        Assert.Equal(100, original.Gold);
        Assert.NotSame(original, richer);
    }

    [Fact]
    public void AddGold_음수면_자금이_줄어든다()
    {
        var faction = new Faction(new FactionId(1), "촉", new GeneralId(2), Gold: 100, Color: "#2c8c46");

        Assert.Equal(70, faction.AddGold(-30).Gold);
    }
}
