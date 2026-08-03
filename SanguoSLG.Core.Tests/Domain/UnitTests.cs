using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Domain;

public class UnitTests
{
    [Fact]
    public void MoveTo_위치를_바꾼_새_부대를_반환하고_원본은_불변이다()
    {
        var original = new Unit(new UnitId(1), new FactionId(1), new HexCoord(0, 0));

        var moved = original.MoveTo(new HexCoord(2, -1));

        Assert.Equal(new HexCoord(2, -1), moved.Position);
        Assert.Equal(new HexCoord(0, 0), original.Position);
        Assert.NotSame(original, moved);
    }
}
