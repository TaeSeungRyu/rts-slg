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

    [Fact]
    public void 소형성_지정한_동쪽_출구가_막히면_다른_방향으로_바꾸지않고_기다린다()
    {
        var city = CityOf(CastleSize.Small);
        var east = DeploymentEgressRules.RepresentativeExit(city, DeploymentDirection.East)!.Value;
        var waiting = Unit(1, city, DeploymentDirection.East, east, 1) with
        { Field = Unit(1, city, DeploymentDirection.East, east, 1).Field with { Target = null } };
        var enemy = Unit(2, city, DeploymentDirection.East, east, 2) with
        {
            Field = waiting.Field with { Id = new UnitId(2), Owner = new FactionId(2), Position = east },
            AwaitingEgress = false,
        };

        var result = DeploymentEgressController.Release([waiting], [enemy], [city], allowFriendlyOverlap: true);

        Assert.Empty(result.Released);
        var queued = Assert.Single(result.Waiting);
        Assert.True(queued.IsWaitingEgress);
        Assert.Equal(DeploymentDirection.East, queued.EgressDirection);
        Assert.Equal(east, queued.EgressExit);
    }

    [Fact]
    public void 목표가_남서인_소형성은_남서_출구가_막혀도_동쪽으로_우회출격하지_않는다()
    {
        var city = CityOf(CastleSize.Small);
        var southWest = DeploymentEgressRules.RepresentativeExit(city, DeploymentDirection.SouthWest)!.Value;
        var waiting = Unit(1, city, DeploymentDirection.SouthWest, southWest, 1) with
        {
            Field = Unit(1, city, DeploymentDirection.SouthWest, southWest, 1).Field with
            { Target = city.Position + new HexCoord(-4, 4) },
        };
        var enemy = Unit(2, city, DeploymentDirection.SouthWest, southWest, 2) with
        {
            Field = waiting.Field with { Id = new UnitId(2), Owner = new FactionId(2),
                Position = southWest, Target = null },
            AwaitingEgress = false,
        };

        var blocked = DeploymentEgressController.Release([waiting], [enemy], [city], allowFriendlyOverlap: true);
        Assert.Empty(blocked.Released);
        Assert.Equal(DeploymentDirection.SouthWest, Assert.Single(blocked.Waiting).EgressDirection);

        var released = DeploymentEgressController.Release(blocked.Waiting, [], [city], allowFriendlyOverlap: true);
        Assert.Equal(southWest, Assert.Single(released.Released).Field.Position);
    }

    [Fact]
    public void 소형성_여섯출구가_전부_막히면_대기열에_남고_빈뒤_출격한다()
    {
        var city = CityOf(CastleSize.Small);
        var east = DeploymentEgressRules.RepresentativeExit(city, DeploymentDirection.East)!.Value;
        var waiting = Unit(1, city, DeploymentDirection.East, east, 1);
        var blockers = DeploymentEgressRules.Directions.Select((direction, index) =>
        {
            var exit = DeploymentEgressRules.RepresentativeExit(city, direction)!.Value;
            return Unit(index + 2, city, direction, exit, index + 2) with
            {
                Field = waiting.Field with { Id = new UnitId(index + 2), Owner = new FactionId(2), Position = exit },
                AwaitingEgress = false,
            };
        }).ToArray();

        var blocked = DeploymentEgressController.Release([waiting], blockers, [city], allowFriendlyOverlap: true);
        Assert.Empty(blocked.Released);
        var queued = Assert.Single(blocked.Waiting);
        Assert.True(queued.IsWaitingEgress);
        Assert.Equal(city.Position, queued.Field.Position);
        Assert.Equal(east, queued.EgressExit);

        var opened = DeploymentEgressController.Release(blocked.Waiting, blockers.Skip(1).ToArray(),
            [city], allowFriendlyOverlap: true);
        Assert.Empty(opened.Waiting);
        Assert.Equal(east, Assert.Single(opened.Released).Field.Position);
    }
}
