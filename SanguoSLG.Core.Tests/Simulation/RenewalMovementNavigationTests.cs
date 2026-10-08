namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;

public sealed class RenewalMovementNavigationTests
{
    private static RenewalUnitState Unit(int id, HexCoord start, HexCoord destination,
        int speed = 1, int faction = 1, MovementDomain domain = MovementDomain.Land) =>
        RenewalUnitState.Create(new UnitId(id), RenewalHexSpace.Center(start),
            RenewalHexSpace.Center(destination), speed) with
        {
            Owner = new FactionId(faction),
            Domain = domain,
        };

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, -2)]
    [InlineData(-3, 5)]
    public void 연속좌표_어댑터는_헥사_중심을_원래_좌표로_복원한다(int q, int r)
    {
        var hex = new HexCoord(q, r);

        Assert.Equal(hex, RenewalHexSpace.NearestHex(RenewalHexSpace.Center(hex)));
    }

    [Fact]
    public void 경로는_건물과_통행불가_지형을_피한다()
    {
        var terrain = new Dictionary<HexCoord, TerrainType>
        {
            [new HexCoord(1, 0)] = TerrainType.WaterDeep,
        };
        var map = new RenewalMovementMap(new HexMap(-2, 4, -3, 3, terrain),
            [new HexCoord(2, -1)]);

        var path = map.FindPath(MovementDomain.Land, RenewalHexSpace.Center(new HexCoord(0, 0)),
            RenewalHexSpace.Center(new HexCoord(3, 0)));
        var tiles = path.Select(RenewalHexSpace.NearestHex).ToList();

        Assert.NotEmpty(path);
        Assert.DoesNotContain(new HexCoord(1, 0), tiles);
        Assert.DoesNotContain(new HexCoord(2, -1), tiles);
        Assert.Equal(new HexCoord(3, 0), tiles[^1]);
    }

    [Fact]
    public void 장애물이_없는_구간은_타일_중심을_경유하지_않고_클릭한_좌표로_직행한다()
    {
        var map = new RenewalMovementMap(new HexMap(-3, 8, -3, 8));
        var start = new ContinuousPosition(115, 85);
        var destination = new ContinuousPosition(1950, 1270);

        var path = map.FindPath(MovementDomain.Land, start, destination);

        Assert.Equal([destination], path);
    }

    [Fact]
    public void 육상과_선박은_서로의_통행영역에_들어갈_수_없다()
    {
        var terrain = new Dictionary<HexCoord, TerrainType>
        {
            [new HexCoord(1, 0)] = TerrainType.WaterDeep,
            [new HexCoord(2, 0)] = TerrainType.WaterDeep,
        };
        var map = new RenewalMovementMap(new HexMap(0, 2, 0, 0, terrain));

        Assert.Empty(map.FindPath(MovementDomain.Land, RenewalHexSpace.Center(new HexCoord(0, 0)),
            RenewalHexSpace.Center(new HexCoord(2, 0))));
        Assert.Empty(map.FindPath(MovementDomain.DeepWater, RenewalHexSpace.Center(new HexCoord(1, 0)),
            RenewalHexSpace.Center(new HexCoord(0, 0))));
    }

    [Fact]
    public void 감속지형에_판정원이_걸치면_가장_강한_감속을_한번만_적용한다()
    {
        var terrain = new Dictionary<HexCoord, TerrainType>
        {
            [new HexCoord(1, 0)] = TerrainType.Swamp,
            [new HexCoord(1, -1)] = TerrainType.Mountain,
        };
        var map = new RenewalMovementMap(new HexMap(-1, 2, -2, 2, terrain));
        var position = new ContinuousPosition(300, 0);

        Assert.Equal(50, map.SpeedPercentAt(position, RenewalAdvanceSimulator.UnitCollisionRadius));
    }

    [Fact]
    public void 경로를_따라도_속도3은_하루에_정확히_3칸_예산을_쓴다()
    {
        var map = new RenewalMovementMap(new HexMap(-2, 8, -3, 3));
        var simulator = new RenewalAdvanceSimulator(map);
        var start = RenewalHexSpace.Center(new HexCoord(0, 0));
        var unit = Unit(1, new HexCoord(0, 0), new HexCoord(6, 0), speed: 3);

        var result = simulator.StepPhase(simulator.Start([unit]));

        var travelled = result.Events.Where(x => x.Kind == RenewalAdvanceEventKind.UnitMoved)
            .Sum(x => x.From!.Value.DistanceTo(x.To!.Value));
        Assert.InRange(travelled, 2_950, 3_050);
        Assert.InRange(start.DistanceTo(result.State.Units[0].Position), 2_500, 3_500);
    }

    [Fact]
    public void 늪에서_시작한_속도1_부대의_첫틱은_평지의_절반인_10단위다()
    {
        var terrain = new Dictionary<HexCoord, TerrainType>
        {
            [new HexCoord(0, 0)] = TerrainType.Swamp,
        };
        var map = new RenewalMovementMap(new HexMap(-3, 5, -3, 3, terrain));
        var simulator = new RenewalAdvanceSimulator(map);
        var unit = Unit(1, new HexCoord(0, 0), new HexCoord(3, 0));

        var result = simulator.StepMovementTick(simulator.Start([unit]));

        Assert.Equal(10, unit.Position.DistanceTo(result.State.Units[0].Position));
    }

    [Fact]
    public void 아군은_이동_장애물이_아니며_같은_목적지에서_분산한다()
    {
        var map = new RenewalMovementMap(new HexMap(-2, 5, -3, 3));
        var simulator = new RenewalAdvanceSimulator(map);
        var units = new[]
        {
            Unit(1, new HexCoord(0, 0), new HexCoord(1, 0), speed: 3),
            Unit(2, new HexCoord(0, 0), new HexCoord(1, 0), speed: 3),
        };

        var result = simulator.StepDay(simulator.Start(units));

        Assert.All(result.State.Units, x => Assert.True(x.Arrived));
        Assert.NotEqual(result.State.Units[0].Position, result.State.Units[1].Position);
        Assert.All(result.State.Units, x => Assert.True(
            x.Position.DistanceTo(x.Destination) <= RenewalAdvanceSimulator.ArrivalDispersionLimit));
        Assert.Contains(result.Events, x => x.Kind == RenewalAdvanceEventKind.UnitDispersed);
    }

    [Fact]
    public void 적군은_막지만_접촉한_적에게서_멀어지는_이동은_허용한다()
    {
        var map = new RenewalMovementMap(new HexMap(-2, 6, -3, 3));
        var simulator = new RenewalAdvanceSimulator(map);
        var mover = Unit(1, new HexCoord(0, 0), new HexCoord(5, 0), speed: 50);
        var enemy = Unit(2, new HexCoord(1, 0), new HexCoord(1, 0), faction: 2);

        var blocked = simulator.StepMovementTick(simulator.Start([mover, enemy]));
        Assert.Equal(RenewalStopReason.EnemyBlocked, blocked.State.Units[0].StopReason);
        Assert.Equal(mover.Position, blocked.State.Units[0].Position);

        var retreat = mover with
        {
            Position = new ContinuousPosition(500, 0),
            Destination = RenewalHexSpace.Center(new HexCoord(-1, 0)),
        };
        var movedAway = simulator.StepMovementTick(simulator.Start([retreat, enemy]));
        Assert.NotEqual(retreat.Position, movedAway.State.Units[0].Position);
    }

    [Fact]
    public void 빠른_이동도_중간의_작은_건물을_관통하지_않는다()
    {
        var obstacle = new RenewalStaticObstacle("watchtower", new ContinuousPosition(350, 0), 60);
        var map = new RenewalMovementMap(new HexMap(-2, 6, -3, 3), obstacles: [obstacle]);
        var simulator = new RenewalAdvanceSimulator(map);
        var mover = Unit(1, new HexCoord(0, 0), new HexCoord(5, 0), speed: 25);

        var result = simulator.StepMovementTick(simulator.Start([mover]));

        Assert.Equal(RenewalStopReason.BuildingBlocked, result.State.Units[0].StopReason);
        Assert.Equal(mover.Position, result.State.Units[0].Position);
    }

    [Fact]
    public void 서로_마주오는_빠른_적군은_같은_틱에도_서로를_관통하지_않는다()
    {
        var map = new RenewalMovementMap(new HexMap(-2, 6, -3, 3));
        var simulator = new RenewalAdvanceSimulator(map);
        var left = Unit(1, new HexCoord(0, 0), new HexCoord(2, 0), speed: 25, faction: 1);
        var right = Unit(2, new HexCoord(1, 0), new HexCoord(-1, 0), speed: 25, faction: 2);

        var result = simulator.StepMovementTick(simulator.Start([left, right]));
        var distance = result.State.Units[0].Position.DistanceTo(result.State.Units[1].Position);

        Assert.True(distance >= RenewalAdvanceSimulator.UnitCollisionRadius * 2);
        Assert.Contains(result.State.Units, x => x.StopReason == RenewalStopReason.EnemyBlocked);
    }

    [Fact]
    public void 한틱에_적을_완전히_지나쳐도_끝점이_멀어진다는_이유로_관통하지않는다()
    {
        var simulator = new RenewalAdvanceSimulator(new RenewalMovementMap(new HexMap(-5, 12, -5, 12)));
        var mover = Unit(1, new(0, 0), new(8, 0), speed: 150);
        var enemy = Unit(2, new(1, 0), new(1, 0), faction: 2);
        var result = simulator.StepMovementTick(simulator.Start([mover, enemy]));

        Assert.Equal(RenewalStopReason.EnemyBlocked, result.State.Units[0].StopReason);
        Assert.Equal(mover.Position, result.State.Units[0].Position);
    }

    [Fact]
    public void 도착허용오차_안의_마지막_위치확정도_적군을_침범하지않는다()
    {
        var simulator = new RenewalAdvanceSimulator(new RenewalMovementMap(new HexMap(-5, 12, -5, 12)));
        var mover = Unit(1, new(0, 0), new(0, 0)) with
        {
            Position = new(0, 0), Destination = new(45, 0), Arrived = false,
        };
        var enemy = Unit(2, new(1, 0), new(1, 0), faction: 2) with
        {
            Position = new(910, 0), Destination = new(910, 0),
        };
        var result = simulator.StepMovementTick(simulator.Start([mover, enemy]));

        Assert.Equal(RenewalStopReason.EnemyBlocked, result.State.Units[0].StopReason);
        Assert.False(result.State.Units[0].Arrived);
        Assert.Equal(mover.Position, result.State.Units[0].Position);
    }

    [Fact]
    public void 기존저장의_작은_논리간격도_실제편대폭만큼_분리하고_아군중첩은_유지한다()
    {
        var simulator = new RenewalAdvanceSimulator(new RenewalMovementMap(new HexMap(-5, 12, -5, 12)));
        var first = Unit(1, new(0, 0), new(0, 0));
        var ally = first with { Id = new(2) };
        var enemy = Unit(3, new(0, 0), new(0, 0), faction: 2) with
        {
            Position = new(155, 338),
        };
        var state = simulator.Start([first, ally, enemy]);

        Assert.Equal(state.Units[0].Position, state.Units[1].Position);
        Assert.True(state.Units[0].Position.DistanceTo(state.Units[2].Position) >= 900);
    }

    [Fact]
    public void 목적지가_막혔으면_경로없음으로_남고_순간이동하지_않는다()
    {
        var destination = new HexCoord(2, 0);
        var map = new RenewalMovementMap(new HexMap(-2, 4, -3, 3), [destination]);
        var simulator = new RenewalAdvanceSimulator(map);
        var unit = Unit(1, new HexCoord(0, 0), destination, speed: 3);

        var result = simulator.StepDay(simulator.Start([unit]));

        Assert.Equal(RenewalStopReason.NoPath, result.State.Units[0].StopReason);
        Assert.Equal(unit.Position, result.State.Units[0].Position);
    }

    [Fact]
    public void 같은_입력은_반복실행해도_경로와_최종위치가_같다()
    {
        var map = new RenewalMovementMap(new HexMap(-2, 5, -3, 3), [new HexCoord(1, 0)]);
        var simulator = new RenewalAdvanceSimulator(map);
        var units = new[]
        {
            Unit(2, new HexCoord(0, 0), new HexCoord(3, 0), speed: 3),
            Unit(1, new HexCoord(0, 0), new HexCoord(3, 0), speed: 3),
        };

        var first = simulator.RunToCompletion(simulator.Start(units));
        var second = simulator.RunToCompletion(simulator.Start(units.Reverse()));

        Assert.Equal(first.State.Units.Select(x => (x.Id, x.Position, x.Arrived, x.PathIndex)),
            second.State.Units.Select(x => (x.Id, x.Position, x.Arrived, x.PathIndex)));
        Assert.Equal(first.Events, second.Events);
    }
}
