using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Spatial;

public class HexMapTests
{
    [Fact]
    public void Count는_경계_내_타일_개수와_같다()
    {
        var map = new HexMap(minQ: 0, maxQ: 2, minR: 0, maxR: 1); // 3 x 2

        Assert.Equal(6, map.Count);
    }

    [Fact]
    public void Contains는_경계_안팎을_구분한다()
    {
        var map = new HexMap(-2, 8, -3, 5);

        Assert.True(map.Contains(new HexCoord(0, 0)));
        Assert.True(map.Contains(new HexCoord(-2, -3)));  // 모서리
        Assert.True(map.Contains(new HexCoord(8, 5)));    // 모서리
        Assert.False(map.Contains(new HexCoord(9, 0)));   // q 초과
        Assert.False(map.Contains(new HexCoord(0, 6)));   // r 초과
    }

    [Fact]
    public void 평평한_필드에서는_경계_안이면_통행_가능하다()
    {
        var map = new HexMap(0, 3, 0, 3);

        Assert.True(map.IsPassable(new HexCoord(1, 2)));
        Assert.False(map.IsPassable(new HexCoord(4, 0)));
    }

    [Fact]
    public void Tiles는_Count개를_모두_경계_안에서_결정론적_순서로_낸다()
    {
        var map = new HexMap(0, 1, 0, 1);

        var tiles = map.Tiles().ToList();

        Assert.Equal(map.Count, tiles.Count);
        Assert.All(tiles, t => Assert.True(map.Contains(t)));
        // q 바깥, r 안쪽 순서
        Assert.Equal(
            new[]
            {
                new HexCoord(0, 0), new HexCoord(0, 1),
                new HexCoord(1, 0), new HexCoord(1, 1),
            },
            tiles);
    }

    [Theory]
    [InlineData(5, 0, 0, 0)]  // maxQ < minQ
    [InlineData(0, 0, 5, 0)]  // maxR < minR
    public void 뒤집힌_경계는_생성_시_예외다(int minQ, int maxQ, int minR, int maxR)
    {
        Assert.Throws<ArgumentException>(() => new HexMap(minQ, maxQ, minR, maxR));
    }
}
