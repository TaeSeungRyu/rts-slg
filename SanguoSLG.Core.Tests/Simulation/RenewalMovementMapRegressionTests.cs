namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;

public sealed class RenewalMovementMapRegressionTests
{
    [Fact]
    public void 건축물_경계에_걸친_부대는_밖으로_빠져나올_수_있다()
    {
        var obstacle = new HexCoord(2, 0);
        var map = new RenewalMovementMap(new HexMap(-3, 5, -3, 3), [obstacle]);
        var center = RenewalHexSpace.Center(obstacle);
        var start = new ContinuousPosition(center.X - 700, center.Y);
        var away = new ContinuousPosition(center.X - 950, center.Y);

        Assert.Equal(RenewalStopReason.None,
            map.FirstStaticCollision(start, away, RenewalAdvanceSimulator.UnitCollisionRadius,
                MovementDomain.Land));
        Assert.Equal(RenewalStopReason.BuildingBlocked,
            map.FirstStaticCollision(start, new ContinuousPosition(center.X - 500, center.Y),
                RenewalAdvanceSimulator.UnitCollisionRadius, MovementDomain.Land));
    }
}
