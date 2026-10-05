namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;
using Xunit;

public sealed class RenewalDeploymentServiceTests
{
    private static City Site(int id, int q, int r, CastleSize size = CastleSize.Small,
        PortSize port = PortSize.None) => new(new CityId(id), $"site-{id}", new HexCoord(q, r),
        new FactionId(1), 1_000, size, Port: port);

    private static RenewalUnitState Unit(int id, City origin, HexCoord destination,
        MovementDomain domain = MovementDomain.Land) =>
        RenewalUnitState.Create(new UnitId(id), RenewalHexSpace.Center(origin.Position),
            RenewalHexSpace.Center(destination), 2) with
        {
            Owner = origin.Owner,
            Domain = domain,
        };

    private static RenewalMovementMap Map(IEnumerable<City> sites,
        IReadOnlyDictionary<HexCoord, TerrainType>? terrain = null) => new(
        new HexMap(0, 30, 0, 20, terrain),
        sites.SelectMany(CastleFootprint.TilesFor));

    [Theory]
    [InlineData(CastleSize.Small)]
    [InlineData(CastleSize.Medium)]
    [InlineData(CastleSize.Large)]
    public void 성_세규모_육방향에서_아군_다섯부대까지_동시출격한다(CastleSize size)
    {
        var city = Site(1, 10, 10, size);
        foreach (var direction in DeploymentEgressRules.Directions)
        {
            var offset = DeploymentEgressRules.Offset(direction);
            var target = new HexCoord(city.Position.Q + offset.Q * 8,
                city.Position.R + offset.R * 8);
            foreach (var count in new[] { 1, 3, 5 })
            {
                var reservations = Enumerable.Range(1, count)
                    .Select(id => new RenewalDeploymentReservation(id,
                        Unit(id, city, target), city.Id))
                    .ToArray();

                var result = new RenewalDeploymentService(Map([city]))
                    .Release(1, reservations, [], [city]);

                Assert.Equal(count, result.Released.Count);
                Assert.Empty(result.Waiting);
                Assert.Single(result.Released.Select(x => x.Position).Distinct());
                var exitHex = RenewalHexSpace.NearestHex(result.Released[0].Position);
                Assert.Contains(exitHex, DeploymentEgressRules.ExitGroup(city, direction));
            }
        }
    }

    [Fact]
    public void 출전지연은_당일부터_육일까지_정확한날에만_해제된다()
    {
        var city = Site(1, 5, 5);
        var service = new RenewalDeploymentService(Map([city]));
        for (var delay = 0; delay <= 6; delay++)
        {
            var reservation = new RenewalDeploymentReservation(delay + 1,
                Unit(delay + 1, city, new HexCoord(12, 5)), city.Id,
                OrderedDay: 3, DelayDays: delay);
            Assert.Empty(service.Release(2 + delay, [reservation], [], [city]).Released);
            Assert.Single(service.Release(3 + delay, [reservation], [], [city]).Released);
        }
    }

    [Fact]
    public void 같은세력은_출구를공유하지만_적군이막으면_예약에남는다()
    {
        var city = Site(1, 5, 5);
        var service = new RenewalDeploymentService(Map([city]));
        var a = new RenewalDeploymentReservation(1, Unit(1, city, new HexCoord(12, 5)), city.Id);
        var b = new RenewalDeploymentReservation(2, Unit(2, city, new HexCoord(12, 5)), city.Id);
        var allies = service.Release(1, [a, b], [], [city]);
        Assert.Equal(2, allies.Released.Count);

        var enemy = allies.Released[0] with { Id = new UnitId(99), Owner = new FactionId(2) };
        var blocked = service.Release(1, [a], [enemy], [city]);
        Assert.Empty(blocked.Released);
        Assert.Single(blocked.Waiting);
        Assert.Contains(blocked.Events, x => x.Kind == RenewalDeploymentEventKind.ExitBlocked);
    }

    [Fact]
    public void 항구는_육상과_해상의_통행가능한_외곽에서만_출격한다()
    {
        var port = Site(1, 5, 5, port: PortSize.Small);
        var water = new Dictionary<HexCoord, TerrainType>
        {
            [new HexCoord(6, 5)] = TerrainType.WaterDeep,
            [new HexCoord(7, 5)] = TerrainType.WaterDeep,
            [new HexCoord(8, 5)] = TerrainType.WaterDeep,
        };
        var service = new RenewalDeploymentService(Map([port], water));
        var land = new RenewalDeploymentReservation(1,
            Unit(1, port, new HexCoord(1, 5)), port.Id);
        var ship = new RenewalDeploymentReservation(2,
            Unit(2, port, new HexCoord(8, 5), MovementDomain.DeepWater), port.Id);

        var result = service.Release(1, [land, ship], [], [port]);

        Assert.Equal(2, result.Released.Count);
        Assert.True(result.Released.Single(x => x.Id.Value == 1).Position.X
            < RenewalHexSpace.Center(port.Position).X);
        Assert.Equal(new HexCoord(6, 5), RenewalHexSpace.NearestHex(
            result.Released.Single(x => x.Id.Value == 2).Position));
    }

    [Fact]
    public void 지정한_거점외곽에_도착해야만_원자적으로_입성하고_집단군을해체한다()
    {
        var origin = Site(1, 4, 5);
        var target = Site(2, 15, 5, CastleSize.Medium);
        var other = Site(3, 10, 5);
        var service = new RenewalDeploymentService(Map([origin, target, other]));
        var payload = new RenewalGarrisonPayload(new Dictionary<string, int>
        {
            ["swordsman"] = 10_000,
            ["archer"] = 8_000,
            ["catapult"] = 5_000,
        }, Provisions: 120, Gold: 300, DissolveArmyGroup: true);
        var reservation = new RenewalDeploymentReservation(1,
            Unit(1, origin, target.Position), origin.Id,
            DestinationSite: target.Id, Payload: payload);
        var release = service.Release(1, [reservation], [], [origin, other, target]);
        var deployed = Assert.Single(release.Released);
        var order = Assert.Single(release.EntryOrders);

        var premature = service.ResolveEntries([deployed], release.EntryOrders,
            [origin, other, target]);
        Assert.Empty(premature.Transfers);

        var arrived = deployed with { Position = order.Approach, Arrived = true };
        var entered = service.ResolveEntries([arrived], release.EntryOrders,
            [origin, other, target]);
        var transfer = Assert.Single(entered.Transfers);
        Assert.Empty(entered.FieldUnits);
        Assert.Equal(target.Id, transfer.Site);
        Assert.Equal(payload, transfer.Payload);
        Assert.Contains(entered.Events,
            x => x.Kind == RenewalDeploymentEventKind.ArmyGroupDissolved);
    }

    [Fact]
    public void 함락된원점과_영일치하지않는복귀대상은_부대를유실시키지않는다()
    {
        var origin = Site(1, 4, 5);
        var captured = origin with { Owner = new FactionId(2) };
        var reservation = new RenewalDeploymentReservation(1,
            Unit(1, origin, new HexCoord(12, 5)), origin.Id);
        var service = new RenewalDeploymentService(Map([captured]));

        var result = service.Release(1, [reservation], [], [captured]);

        Assert.Empty(result.Released);
        Assert.Single(result.Waiting);
    }

    [Fact]
    public void 적으로바뀐_목적지와_통행가능한외곽이없는항구는_출격하지않는다()
    {
        var origin = Site(1, 4, 5);
        var hostileTarget = Site(2, 15, 5) with { Owner = new FactionId(2) };
        var dryPort = Site(3, 22, 5, port: PortSize.Small);
        var service = new RenewalDeploymentService(Map([origin, hostileTarget, dryPort]));
        var hostileOrder = new RenewalDeploymentReservation(1,
            Unit(1, origin, hostileTarget.Position), origin.Id,
            DestinationSite: hostileTarget.Id);
        var shipOrder = new RenewalDeploymentReservation(2,
            Unit(2, dryPort, new HexCoord(28, 5), MovementDomain.DeepWater), dryPort.Id);

        var result = service.Release(1, [hostileOrder, shipOrder], [],
            [origin, hostileTarget, dryPort]);

        Assert.Empty(result.Released);
        Assert.Equal(2, result.Waiting.Count);
        Assert.Empty(result.EntryOrders);
    }
}
