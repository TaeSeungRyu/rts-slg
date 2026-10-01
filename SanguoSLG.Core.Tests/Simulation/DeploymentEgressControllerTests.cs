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
            for (var count = 1; count <= 5; count++)
            {
                var exit = DeploymentEgressRules.RepresentativeExit(city, direction)!.Value;
                var queue = Enumerable.Range(1, count).Select(id => Unit(id, city, direction, exit, order: id)).ToArray();

                var result = DeploymentEgressController.Release(queue, [], [city]);

                Assert.Equal(new UnitId(1), Assert.Single(result.Released).Id);
                Assert.Equal(exit, result.Released[0].Field.Position);
                Assert.False(result.Released[0].AwaitingEgress);
                Assert.Equal(count - 1, result.Waiting.Count);
                Assert.All(result.Waiting, unit => Assert.True(unit.IsWaitingEgress));
            }
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

    [Fact]
    public void 항구와성의_각각막힌출구는_독립대기하고_빈출구만출격한다()
    {
        var city = CityOf(CastleSize.Medium);
        var port = CityOf(CastleSize.Small) with
        {
            Id = new CityId(2), Position = new HexCoord(30, 10), Port = PortSize.Small,
        };
        var cityExit = DeploymentEgressRules.RepresentativeExit(city, DeploymentDirection.East)!.Value;
        var portExit = DeploymentEgressRules.RepresentativeExit(port, DeploymentDirection.West)!.Value;
        var portNorth = DeploymentEgressRules.RepresentativeExit(port, DeploymentDirection.NorthEast)!.Value;
        var cityHead = Unit(1, city, DeploymentDirection.East, cityExit, 1);
        var portHead = Unit(2, port, DeploymentDirection.West, portExit, 2);
        var portOther = Unit(3, port, DeploymentDirection.NorthEast, portNorth, 3);
        var blocker = Unit(99, port, DeploymentDirection.West, portExit, 99) with
        {
            Field = Unit(99, port, DeploymentDirection.West, portExit, 99).Field with { Position = portExit },
            AwaitingEgress = false,
        };

        var result = DeploymentEgressController.Release([cityHead, portHead, portOther], [blocker], [city, port]);
        Assert.Equal(new[] { cityHead.Id, portOther.Id }, result.Released.Select(unit => unit.Id));
        Assert.Equal(portHead.Id, Assert.Single(result.Waiting).Id);
        Assert.Equal(port.Position, result.Waiting[0].Field.Position);
    }

    [Fact]
    public void 잘못된출구와_함락된원점의부대는_임의위치에서출격하지않는다()
    {
        var city = CityOf(CastleSize.Medium);
        var east = DeploymentEgressRules.RepresentativeExit(city, DeploymentDirection.East)!.Value;
        var west = DeploymentEgressRules.RepresentativeExit(city, DeploymentDirection.West)!.Value;
        var invalid = Unit(1, city, DeploymentDirection.East, west, 1);
        var captured = Unit(2, city, DeploymentDirection.East, east, 2);
        var result = DeploymentEgressController.Release([invalid, captured], [],
            [city with { Owner = new FactionId(2) }]);
        Assert.Empty(result.Released);
        Assert.Equal(new[] { invalid.Id, captured.Id }, result.Waiting.Select(unit => unit.Id));
    }

    [Fact]
    public void 항구출구가_해상병종에통행불가하면_빈칸이어도대기열에남는다()
    {
        var port = CityOf(CastleSize.Small) with { Port = PortSize.Small };
        var exit = DeploymentEgressRules.RepresentativeExit(port, DeploymentDirection.East)!.Value;
        var ship = Unit(1, port, DeploymentDirection.East, exit, 1) with
        {
            Field = Unit(1, port, DeploymentDirection.East, exit, 1).Field with { Domain = MovementDomain.DeepWater },
        };
        var blocked = DeploymentEgressController.Release([ship], [], [port], (_, _) => false);
        Assert.Empty(blocked.Released);
        Assert.Equal(ship, Assert.Single(blocked.Waiting));

        var released = DeploymentEgressController.Release(blocked.Waiting, [], [port], (_, _) => true);
        Assert.Equal(exit, Assert.Single(released.Released).Field.Position);
    }
}
