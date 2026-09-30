namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class DeploymentEgressControllerTests
{
    private static City CityOf(CastleSize size) =>
        new(new CityId(1), "test", new HexCoord(10, 10), new FactionId(1), 1000, size);

    private static CombatUnit Unit(int id, City city, DeploymentDirection direction, HexCoord exit, int order)
        => new(
            new FieldUnit(new UnitId(id), city.Owner, city.Position, 2, 2, 1, MovementDomain.Land,
                UnitMode.March, new HexCoord(20, 10), order),
            new CombatStats(1000, 10, 10), new TroopPool(1000, 0), UnitCombatState.Create(60),
            OriginCity: city.Id, EgressDirection: direction, EgressExit: exit, AwaitingEgress: true);

    [Theory]
    [InlineData(CastleSize.Small)]
    [InlineData(CastleSize.Medium)]
    [InlineData(CastleSize.Large)]
    public void 성규모_육방향마다_FIFO_머리만_출격한다(CastleSize size)
    {
        var city = CityOf(size);
        foreach (var direction in DeploymentEgressRules.Directions)
        {
            var exit = DeploymentEgressRules.RepresentativeExit(city, direction)!.Value;
            var queue = Enumerable.Range(1, 5).Select(id => Unit(id, city, direction, exit, order: id)).ToArray();

            var result = DeploymentEgressController.Release(queue, [], [city]);

            Assert.Equal(new UnitId(1), Assert.Single(result.Released).Id);
            Assert.Equal(exit, result.Released[0].Field.Position);
            Assert.False(result.Released[0].AwaitingEgress);
            Assert.Equal(4, result.Waiting.Count);
            Assert.All(result.Waiting, unit => Assert.True(unit.IsWaitingEgress));
        }
    }

    [Fact]
    public void 출구가_점유되면_전원_거점안에서_기다리고_해제후_순차출격한다()
    {
        var city = CityOf(CastleSize.Large);
        var direction = DeploymentDirection.East;
        var exit = DeploymentEgressRules.RepresentativeExit(city, direction)!.Value;
        var queue = new[] { Unit(2, city, direction, exit, 20), Unit(1, city, direction, exit, 10) };
        var blocker = Unit(99, city, direction, exit, 99) with
        {
            Field = Unit(99, city, direction, exit, 99).Field with { Position = exit },
            AwaitingEgress = false,
        };

        var blocked = DeploymentEgressController.Release(queue, [blocker], [city]);
        Assert.Empty(blocked.Released);
        Assert.Equal(2, blocked.Waiting.Count);

        var first = DeploymentEgressController.Release(blocked.Waiting, [], [city]);
        Assert.Equal(new UnitId(1), Assert.Single(first.Released).Id);
        var second = DeploymentEgressController.Release(first.Waiting, [], [city]);
        Assert.Equal(new UnitId(2), Assert.Single(second.Released).Id);
    }

    [Fact]
    public void 서로_다른_방향은_같은날_각각_한부대씩_출격한다()
    {
        var city = CityOf(CastleSize.Medium);
        var east = DeploymentEgressRules.RepresentativeExit(city, DeploymentDirection.East)!.Value;
        var west = DeploymentEgressRules.RepresentativeExit(city, DeploymentDirection.West)!.Value;
        var waiting = new[]
        {
            Unit(1, city, DeploymentDirection.East, east, 1),
            Unit(2, city, DeploymentDirection.East, east, 2),
            Unit(3, city, DeploymentDirection.West, west, 3),
        };

        var result = DeploymentEgressController.Release(waiting, [], [city]);
        Assert.Equal(new[] { new UnitId(1), new UnitId(3) }, result.Released.Select(unit => unit.Id));
        Assert.Equal(new UnitId(2), Assert.Single(result.Waiting).Id);
    }
}
