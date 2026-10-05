namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalAdvanceSimulatorTests
{
    private static RenewalUnitState Unit(long destinationX = 20_000, int speed = 1)
        => RenewalUnitState.Create(new UnitId(1), new ContinuousPosition(0, 0),
            new ContinuousPosition(destinationX, 0), speed);

    [Fact]
    public void 이동_50틱은_정확히_하루_속도만큼_이동하고_후처리로_전환한다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var state = simulator.Start([Unit()]);

        var result = simulator.StepPhase(state);

        Assert.Equal(RenewalAdvancePhase.MovementAftermath, result.State.Phase);
        Assert.Equal(RenewalAdvanceSimulator.MovementTicksPerDay, result.State.MovementTick);
        Assert.Equal(ContinuousPosition.UnitsPerTile, result.State.Units[0].Position.X);
        Assert.Contains(result.Events, x => x.Kind == RenewalAdvanceEventKind.PhaseChanged
            && x.Phase == RenewalAdvancePhase.MovementAftermath);
    }

    [Fact]
    public void 적이_없어도_하루의_모든_단계를_순서대로_통과한다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var result = simulator.StepDay(simulator.Start([Unit()]));
        var phases = result.Events
            .Where(x => x.Kind == RenewalAdvanceEventKind.PhaseChanged)
            .Select(x => x.Phase)
            .ToList();

        Assert.Equal([
            RenewalAdvancePhase.MovementAftermath,
            RenewalAdvancePhase.Attack,
            RenewalAdvancePhase.AttackAftermath,
            RenewalAdvancePhase.DaySettlement,
            RenewalAdvancePhase.Movement,
        ], phases);
        Assert.Equal(2, result.State.Day);
        Assert.Equal(RenewalAdvancePhase.Movement, result.State.Phase);
        Assert.Contains(result.Events, x => x.Kind == RenewalAdvanceEventKind.DayCompleted
            && x.Day == 1);
    }

    [Fact]
    public void 진행은_정확히_7일_정산_뒤_완료되고_추가호출은_상태를_바꾸지_않는다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var completed = simulator.RunToCompletion(simulator.Start([Unit()]));

        Assert.True(completed.State.IsCompleted);
        Assert.Equal(7, completed.State.Day);
        Assert.Equal(7, completed.Events.Count(x => x.Kind == RenewalAdvanceEventKind.DayCompleted));
        Assert.Single(completed.Events, x => x.Kind == RenewalAdvanceEventKind.AdvanceCompleted);

        var after = simulator.StepDay(completed.State);
        Assert.Same(completed.State, after.State);
        Assert.Empty(after.Events);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void 같은_5초를_프레임률별로_나누어도_같은_50틱_결과를_낸다(int fps)
    {
        var simulator = new RenewalAdvanceSimulator();
        var state = simulator.Start([Unit(speed: 3)]);
        var clock = new RenewalFixedStepClock();
        const long totalMicroseconds = 5_000_000;
        var frames = fps * 5;
        var baseFrame = totalMicroseconds / frames;
        var remainder = totalMicroseconds % frames;

        for (var frame = 0; frame < frames; frame++)
        {
            var elapsed = baseFrame + (frame < remainder ? 1 : 0);
            var advanced = simulator.AdvanceElapsed(state, clock, elapsed);
            state = advanced.State;
            clock = advanced.Clock;
        }

        Assert.Equal(RenewalAdvancePhase.MovementAftermath, state.Phase);
        Assert.Equal(50, state.MovementTick);
        Assert.Equal(3_000, state.Units[0].Position.X);
        Assert.Equal(0, clock.PendingMicroseconds);
    }

    [Fact]
    public void 입력순서가_달라도_ID순으로_같은_상태와_사건을_낸다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var one = Unit() with { Id = new UnitId(1) };
        var two = Unit(speed: 2) with { Id = new UnitId(2) };

        var forward = simulator.StepDay(simulator.Start([one, two]));
        var reverse = simulator.StepDay(simulator.Start([two, one]));

        Assert.Equal(forward.State.Day, reverse.State.Day);
        Assert.Equal(forward.State.Phase, reverse.State.Phase);
        Assert.Equal(forward.State.MovementTick, reverse.State.MovementTick);
        Assert.Equal(forward.State.Units, reverse.State.Units);
        Assert.Equal(forward.Events, reverse.Events);
    }

    [Fact]
    public void 새로_시작하면_진행중_상태와_무관하게_초기상태로_복원된다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var initialUnits = new[] { Unit() };
        var progressed = simulator.StepDay(simulator.Start(initialUnits)).State;

        var reset = simulator.Start(initialUnits);

        Assert.NotEqual(progressed, reset);
        Assert.Equal(1, reset.Day);
        Assert.Equal(0, reset.MovementTick);
        Assert.Equal(new ContinuousPosition(0, 0), reset.Units[0].Position);
    }
}
