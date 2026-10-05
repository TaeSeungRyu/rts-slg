namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalOrderLifecycleTests
{
    private static RenewalUnitState Unit(int id, int faction, long x, long y = 0,
        int speed = 3) => RenewalUnitState.Create(new UnitId(id),
        new ContinuousPosition(x, y), new ContinuousPosition(10_000, y), speed) with
        {
            Owner = new FactionId(faction),
            DetectionRange = 3,
            AttackRange = 1,
        };

    [Theory]
    [InlineData(999, true)]
    [InlineData(1_000, true)]
    [InlineData(1_001, false)]
    public void 공격사거리는_경계를_포함한다(long distance, bool inRange)
    {
        var simulator = new RenewalAdvanceSimulator();
        var attacker = Unit(1, 1, 0) with
        {
            Mode = RenewalOrderMode.Attack,
            AssignedTarget = RenewalTargetId.ForUnit(new UnitId(2)),
        };
        var target = Unit(2, 2, distance, speed: 0) with { Mode = RenewalOrderMode.Standby };

        var result = simulator.StepMovementTick(simulator.Start([attacker, target]));
        var actual = result.State.Units.Single(x => x.Id == attacker.Id);

        Assert.Equal(inRange, actual.StopReason == RenewalStopReason.TargetInRange);
        Assert.Equal(inRange ? 0 : 60, actual.Position.X);
    }

    [Fact]
    public void 행군은_옆의_적을_탐지해도_목적지와_모드를_유지한다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var marcher = Unit(1, 1, 0) with { Mode = RenewalOrderMode.March };
        var enemy = Unit(2, 2, 1_000, 2_000, 0) with { Mode = RenewalOrderMode.Standby };

        var result = simulator.StepMovementTick(simulator.Start([marcher, enemy]));
        var actual = result.State.Units.Single(x => x.Id == marcher.Id);

        Assert.Equal(RenewalOrderMode.March, actual.Mode);
        Assert.Null(actual.PursuitTarget);
        Assert.Equal(marcher.Destination, actual.Destination);
        Assert.DoesNotContain(result.Events, x => x.Kind == RenewalAdvanceEventKind.TargetAcquired);
    }

    [Fact]
    public void 전진은_가장_가까운_적을_ID_동률순으로_추격하고_위치가_바뀌면_갱신한다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var advance = Unit(10, 1, 0) with
        {
            Mode = RenewalOrderMode.Advance,
            OriginalDestination = new ContinuousPosition(10_000, 0),
        };
        var enemyTwo = Unit(2, 2, 2_000, 500, 0) with { Mode = RenewalOrderMode.Standby };
        var enemyThree = Unit(3, 2, 2_000, -500, 0) with { Mode = RenewalOrderMode.Standby };
        var first = simulator.StepMovementTick(simulator.Start([advance, enemyThree, enemyTwo]));
        var pursuing = first.State.Units.Single(x => x.Id == advance.Id);

        Assert.Equal(RenewalTargetId.ForUnit(enemyTwo.Id), pursuing.PursuitTarget);
        Assert.Contains(first.Events, x => x.Kind == RenewalAdvanceEventKind.TargetAcquired
            && x.Target == RenewalTargetId.ForUnit(enemyTwo.Id));

        var movedEnemy = first.State.Units.Single(x => x.Id == enemyTwo.Id) with
        {
            Position = new ContinuousPosition(2_300, 700),
        };
        var changed = first.State with
        {
            Units = first.State.Units.Select(x => x.Id == enemyTwo.Id ? movedEnemy : x).ToList(),
        };
        var second = simulator.StepMovementTick(changed);
        var followed = second.State.Units.Single(x => x.Id == advance.Id);

        Assert.Equal(movedEnemy.Position, followed.Destination);
        Assert.Equal(RenewalTargetId.ForUnit(enemyTwo.Id), followed.PursuitTarget);
    }

    [Fact]
    public void 전진은_추격대상이_시야에서_사라지면_원래_목적지로_복귀한다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var original = new ContinuousPosition(10_000, 0);
        var advance = Unit(1, 1, 0) with
        {
            Mode = RenewalOrderMode.Advance,
            OriginalDestination = original,
            OriginalWaypoints = [new ContinuousPosition(5_000, 0)],
            PursuitTarget = RenewalTargetId.ForUnit(new UnitId(2)),
            Destination = new ContinuousPosition(2_000, 0),
        };
        var hidden = Unit(2, 2, 2_000, 0, 0) with
        {
            Mode = RenewalOrderMode.Standby,
            IsVisible = false,
        };

        var result = simulator.StepMovementTick(simulator.Start([advance, hidden]));
        var actual = result.State.Units.Single(x => x.Id == advance.Id);

        Assert.Null(actual.PursuitTarget);
        Assert.Equal(new ContinuousPosition(5_000, 0), actual.Destination);
        Assert.Single(actual.OriginalWaypoints!);
        Assert.Contains(result.Events, x => x.Kind == RenewalAdvanceEventKind.TargetLost);
    }

    [Fact]
    public void 전진은_목표를_잃은_같은틱에_다른목표로_흔들리지_않는다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var original = new ContinuousPosition(10_000, 0);
        var advance = Unit(1, 1, 0) with
        {
            Mode = RenewalOrderMode.Advance,
            OriginalDestination = original,
            PursuitTarget = RenewalTargetId.ForUnit(new UnitId(2)),
            Destination = new ContinuousPosition(2_000, 0),
        };
        var lost = Unit(2, 2, 2_000, 0, 0) with
        {
            Mode = RenewalOrderMode.Standby,
            IsVisible = false,
        };
        var other = Unit(3, 2, 1_500, 500, 0) with { Mode = RenewalOrderMode.Standby };

        var result = simulator.StepMovementTick(simulator.Start([advance, lost, other]));
        var actual = result.State.Units.Single(x => x.Id == advance.Id);

        Assert.Null(actual.PursuitTarget);
        Assert.Equal(original, actual.Destination);
        Assert.DoesNotContain(result.Events, x => x.Kind == RenewalAdvanceEventKind.TargetAcquired);
    }

    [Fact]
    public void 행군은_경유지를_순서대로_소비한뒤_원래_목적지로_간다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var service = new RenewalCommandService();
        var state = simulator.Start([Unit(1, 1, 0, speed: 10)]);
        state = service.Apply(state, new RenewalUnitCommand(1, new UnitId(1),
            RenewalOrderMode.March, new ContinuousPosition(3_000, 0),
            Waypoints: [new ContinuousPosition(1_000, 0), new ContinuousPosition(2_000, 0)]));

        state = simulator.StepDay(state).State;
        var actual = state.Units.Single();

        Assert.Equal(new ContinuousPosition(3_000, 0), actual.Position);
        Assert.Equal(2, actual.OriginalWaypointIndex);
        Assert.Equal(RenewalOrderMode.Standby, actual.Mode);
    }

    [Fact]
    public void 공격은_지정대상만_추적하고_시야상실시_마지막위치에서_대기한다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var attacker = Unit(1, 1, 0) with
        {
            Mode = RenewalOrderMode.Attack,
            AssignedTarget = RenewalTargetId.ForUnit(new UnitId(2)),
            LastKnownTargetPosition = new ContinuousPosition(2_000, 0),
            Destination = new ContinuousPosition(2_000, 0),
        };
        var assigned = Unit(2, 2, 2_000, 0, 0) with
        {
            Mode = RenewalOrderMode.Standby,
            IsVisible = false,
        };
        var ignored = Unit(3, 2, 500, 500, 0) with { Mode = RenewalOrderMode.Standby };
        var state = simulator.Start([attacker, assigned, ignored]);

        RenewalUnitState actual = null!;
        for (var tick = 0; tick < 50; tick++)
        {
            state = simulator.StepMovementTick(state).State;
            actual = state.Units.Single(x => x.Id == attacker.Id);
            if (actual.Mode == RenewalOrderMode.Standby)
            {
                break;
            }
        }

        Assert.Equal(RenewalOrderMode.Standby, actual.Mode);
        Assert.Equal(RenewalStopReason.TargetLost, actual.StopReason);
        Assert.True(actual.Position.DistanceTo(new ContinuousPosition(2_000, 0))
            <= RenewalAdvanceSimulator.UnitCollisionRadius * 2 + 100);
        Assert.NotEqual(ignored.Position, actual.Destination);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public void 공격대상이_제거되거나_우호화되면_즉시_대기한다(bool active, int faction)
    {
        var simulator = new RenewalAdvanceSimulator();
        var attacker = Unit(1, 1, 0) with
        {
            Mode = RenewalOrderMode.Attack,
            AssignedTarget = RenewalTargetId.ForUnit(new UnitId(2)),
            LastKnownTargetPosition = new ContinuousPosition(2_000, 0),
        };
        var target = Unit(2, faction, 2_000, 0, 0) with
        {
            Mode = RenewalOrderMode.Standby,
            IsActive = active,
        };

        var result = simulator.StepMovementTick(simulator.Start([attacker, target]));
        var actual = result.State.Units.Single(x => x.Id == attacker.Id);

        Assert.Equal(RenewalOrderMode.Standby, actual.Mode);
        Assert.Equal(RenewalStopReason.TargetLost, actual.StopReason);
        Assert.Contains(result.Events, x => x.Kind == RenewalAdvanceEventKind.TargetLost);
    }

    [Fact]
    public void 공격은_건축물과_거점도_공통_ID로_추적하고_우호화되면_중단한다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var siteId = new RenewalTargetId(RenewalTargetKind.Site, 77);
        var attacker = Unit(1, 1, 0) with
        {
            Mode = RenewalOrderMode.Attack,
            AssignedTarget = siteId,
            LastKnownTargetPosition = new ContinuousPosition(2_000, 0),
        };
        var hostile = new RenewalTargetState(siteId, new FactionId(2),
            new ContinuousPosition(2_000, 0));
        var state = simulator.Start([attacker], [hostile]);

        var tracking = simulator.StepMovementTick(state).State;
        Assert.Equal(siteId, tracking.Units.Single().AssignedTarget);
        Assert.Equal(hostile.Position, tracking.Units.Single().Destination);

        var friendly = hostile with { Owner = new FactionId(1) };
        var changed = tracking with { ExternalTargets = [friendly] };
        var stopped = simulator.StepMovementTick(changed).State.Units.Single();

        Assert.Equal(RenewalOrderMode.Standby, stopped.Mode);
        Assert.Equal(RenewalStopReason.TargetLost, stopped.StopReason);
    }

    [Fact]
    public void 명령은_부대별_ID로_한번만_적용되고_두번째_부대를_덮어쓰지_않는다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var service = new RenewalCommandService();
        var state = simulator.Start([Unit(1, 1, 0), Unit(2, 1, 0, 2_000)]);
        var first = new RenewalUnitCommand(10, new UnitId(1), RenewalOrderMode.March,
            new ContinuousPosition(8_000, 0));
        var second = new RenewalUnitCommand(10, new UnitId(2), RenewalOrderMode.Advance,
            new ContinuousPosition(8_000, 2_000));

        state = service.Apply(state, first);
        state = service.Apply(state, first with { Destination = new ContinuousPosition(1_000, 0) });
        state = service.Apply(state, second);

        Assert.Equal(new ContinuousPosition(8_000, 0), state.Units.Single(x => x.Id.Value == 1).Destination);
        Assert.Equal(RenewalOrderMode.Advance, state.Units.Single(x => x.Id.Value == 2).Mode);
        Assert.Equal(new ContinuousPosition(8_000, 2_000),
            state.Units.Single(x => x.Id.Value == 2).Destination);
    }
}
