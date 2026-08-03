using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Spatial;

public class HexPathfinderTests
{
    private static readonly HexMap Field = new(-5, 5, -5, 5);

    private static void AssertValidPath(IReadOnlyList<HexCoord> path, HexCoord start, HexCoord goal)
    {
        Assert.NotEmpty(path);
        Assert.Equal(start, path[0]);
        Assert.Equal(goal, path[^1]);
        // 연속한 타일은 서로 인접(거리 1)해야 한다.
        for (var i = 1; i < path.Count; i++)
        {
            Assert.Equal(1, path[i - 1].Distance(path[i]));
        }
    }

    [Fact]
    public void 평평한_필드에서_최단경로_길이는_헥사거리_더하기_1이다()
    {
        var pathfinder = new HexPathfinder(Field);
        var start = new HexCoord(0, 0);
        var goal = new HexCoord(3, 0);

        var path = pathfinder.FindPath(start, goal);

        AssertValidPath(path, start, goal);
        Assert.Equal(start.Distance(goal) + 1, path.Count);
    }

    [Fact]
    public void 시작과_목표가_같으면_자기자신만_반환한다()
    {
        var pathfinder = new HexPathfinder(Field);
        var here = new HexCoord(2, -1);

        Assert.Equal(new[] { here }, pathfinder.FindPath(here, here));
    }

    [Fact]
    public void 장애물을_피해서_목표에_도달한다()
    {
        var start = new HexCoord(0, 0);
        var goal = new HexCoord(2, 0);

        // 직선 경로의 중간 타일 (1,0)을 막는다.
        var blocked = new HashSet<HexCoord> { new(1, 0) };
        var pathfinder = new HexPathfinder(c => Field.Contains(c) && !blocked.Contains(c));

        var path = pathfinder.FindPath(start, goal);

        AssertValidPath(path, start, goal);
        Assert.DoesNotContain(new HexCoord(1, 0), path);
        // 우회하므로 직선(3칸)보다 길거나 같다.
        Assert.True(path.Count >= start.Distance(goal) + 1);
    }

    [Fact]
    public void 목표가_통행불가면_빈_경로다()
    {
        var start = new HexCoord(0, 0);
        var goal = new HexCoord(2, 0);
        var pathfinder = new HexPathfinder(c => Field.Contains(c) && c != goal);

        Assert.Empty(pathfinder.FindPath(start, goal));
    }

    [Fact]
    public void 목표가_맵_밖이면_빈_경로다()
    {
        var pathfinder = new HexPathfinder(Field);

        Assert.Empty(pathfinder.FindPath(new HexCoord(0, 0), new HexCoord(99, 99)));
    }

    [Fact]
    public void 완전히_둘러싸이면_도달_불가로_빈_경로다()
    {
        var start = new HexCoord(0, 0);
        var goal = new HexCoord(3, 0);
        // start의 6방향 이웃을 전부 막아 가둔다.
        var blocked = start.Neighbors().ToHashSet();
        var pathfinder = new HexPathfinder(c => Field.Contains(c) && !blocked.Contains(c));

        Assert.Empty(pathfinder.FindPath(start, goal));
    }

    [Fact]
    public void 같은_입력은_같은_경로를_낸다()
    {
        var pathfinder = new HexPathfinder(Field);
        var start = new HexCoord(-3, 2);
        var goal = new HexCoord(4, -1);

        Assert.Equal(pathfinder.FindPath(start, goal), pathfinder.FindPath(start, goal));
    }
}
