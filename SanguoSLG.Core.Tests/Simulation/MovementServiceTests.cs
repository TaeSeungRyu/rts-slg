using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Simulation;

public class MovementServiceTests
{
    private static readonly HexMap Field = new(-5, 5, -5, 5);

    private static Unit UnitAt(HexCoord position) => new(new UnitId(1), new FactionId(1), position);

    [Fact]
    public void MoveTo_목표까지_경로를_따라_이동한다()
    {
        var service = new MovementService(Field);
        var unit = UnitAt(new HexCoord(0, 0));
        var target = new HexCoord(3, -1);

        var result = service.MoveTo(unit, target);

        Assert.True(result.Moved);
        Assert.Equal(target, result.Unit.Position);
        Assert.Equal(unit.Position, result.Path[0]);
        Assert.Equal(target, result.Path[^1]);
    }

    [Fact]
    public void MoveTo_도달_불가면_원위치_유지하고_이동하지_않는다()
    {
        var service = new MovementService(Field);
        var unit = UnitAt(new HexCoord(0, 0));

        var result = service.MoveTo(unit, new HexCoord(99, 99)); // 맵 밖

        Assert.False(result.Moved);
        Assert.Equal(new HexCoord(0, 0), result.Unit.Position);
        Assert.Empty(result.Path);
    }

    [Fact]
    public void MoveTo_제자리_목표면_위치가_그대로다()
    {
        var service = new MovementService(Field);
        var unit = UnitAt(new HexCoord(1, 1));

        var result = service.MoveTo(unit, new HexCoord(1, 1));

        Assert.True(result.Moved);
        Assert.Equal(new HexCoord(1, 1), result.Unit.Position);
        Assert.Single(result.Path);
    }
}
