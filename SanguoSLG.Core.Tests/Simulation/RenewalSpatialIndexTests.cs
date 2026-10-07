namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalSpatialIndexTests
{
    [Fact]
    public void QuerySegment_ReturnsOnlyNearbyCandidatesInStableOrder()
    {
        var units = new[]
        {
            Unit(9, 250, 0), Unit(2, 100, 0), Unit(5, 9000, 9000),
        };
        var index = new RenewalSpatialIndex(units, 500);

        var result = index.QuerySegment(new(0, 0), new(300, 0), 100);

        Assert.Equal([2, 9], result.Select(x => x.Id.Value));
    }

    [Fact]
    public void Update_MovesCandidateBetweenCells()
    {
        var unit = Unit(1, 0, 0);
        var index = new RenewalSpatialIndex([unit], 500);
        index.Update(unit with { Position = new(4000, 0) });

        Assert.Empty(index.QuerySegment(new(0, 0), new(100, 0), 50));
        Assert.Single(index.QuerySegment(new(3900, 0), new(4100, 0), 50));
    }

    private static RenewalUnitState Unit(int id, long x, long y) =>
        RenewalUnitState.Create(new UnitId(id), new(x, y), new(x, y), 3);
}
