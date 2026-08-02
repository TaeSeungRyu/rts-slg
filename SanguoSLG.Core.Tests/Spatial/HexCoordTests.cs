using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Spatial;

public class HexCoordTests
{
    [Fact]
    public void Distance_같은좌표면_0이다()
    {
        var a = new HexCoord(2, -1);
        Assert.Equal(0, a.Distance(a));
    }

    [Theory]
    [InlineData(0, 0, 1, 0, 1)]
    [InlineData(0, 0, 2, -1, 2)]
    [InlineData(0, 0, -3, 1, 3)]
    [InlineData(1, -2, -2, 3, 5)]
    public void Distance_두좌표사이_헥사거리를계산하고_대칭이다(int q1, int r1, int q2, int r2, int expected)
    {
        var a = new HexCoord(q1, r1);
        var b = new HexCoord(q2, r2);
        Assert.Equal(expected, a.Distance(b));
        Assert.Equal(expected, b.Distance(a));
    }

    [Fact]
    public void Neighbors_이웃은6개이고_모두거리1이다()
    {
        var center = new HexCoord(0, 0);
        var neighbors = center.Neighbors().ToList();
        Assert.Equal(6, neighbors.Count);
        Assert.All(neighbors, n => Assert.Equal(1, center.Distance(n)));
    }

    [Fact]
    public void Neighbors_고정된순서를반환한다()
    {
        var center = new HexCoord(0, 0);
        var expected = new[]
        {
            new HexCoord(1, 0), new HexCoord(1, -1), new HexCoord(0, -1),
            new HexCoord(-1, 0), new HexCoord(-1, 1), new HexCoord(0, 1),
        };
        Assert.Equal(expected, center.Neighbors());
    }

    [Fact]
    public void S축은_q_r_s의_합이_0이다()
    {
        var h = new HexCoord(3, -5);
        Assert.Equal(0, h.Q + h.R + h.S);
    }
}
