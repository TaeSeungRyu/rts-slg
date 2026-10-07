namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalAdversarialIntegrationTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void 아군30부대가_중첩되어도_프레임률별_결과가_같다(int fps)
    {
        var simulator = new RenewalAdvanceSimulator();
        var units = Enumerable.Range(1, 30).Select(id =>
            RenewalUnitState.Create(new UnitId(id), new(0, 0), new(12_000, 0), 3)
            with { Owner = new FactionId(1) }).ToArray();
        var state = simulator.Start(units);
        var clock = new RenewalFixedStepClock();
        const long total = 5_000_000;
        var frames = fps * 5;
        var frameTime = total / frames;
        var remainder = total % frames;

        for (var frame = 0; frame < frames; frame++)
        {
            var result = simulator.AdvanceElapsed(state, clock,
                frameTime + (frame < remainder ? 1 : 0));
            state = result.State;
            clock = result.Clock;
        }

        Assert.Equal(RenewalAdvancePhase.MovementAftermath, state.Phase);
        Assert.All(state.Units, unit => Assert.Equal(new ContinuousPosition(3_000, 0), unit.Position));
        Assert.Equal(0, clock.PendingMicroseconds);
    }

    [Fact]
    public void 이동중_저장복원은_나머지_틱과_사건을_바꾸지않는다()
    {
        var simulator = new RenewalAdvanceSimulator();
        var state = simulator.Start([
            RenewalUnitState.Create(new UnitId(1), new(0, 0), new(20_000, 0), 3),
            RenewalUnitState.Create(new UnitId(2), new(0, 0), new(20_000, 0), 2),
        ]) with { RandomState = 13579 };
        for (var tick = 0; tick < 17; tick++) state = simulator.StepMovementTick(state).State;

        var restored = new RenewalMovementSaveService().Deserialize(
            new RenewalMovementSaveService().Serialize(state));
        var expected = simulator.StepPhase(state);
        var actual = simulator.StepPhase(restored);

        Assert.Equal(expected.State.Day, actual.State.Day);
        Assert.Equal(expected.State.Phase, actual.State.Phase);
        Assert.Equal(expected.State.MovementTick, actual.State.MovementTick);
        Assert.Equal(expected.State.RandomState, actual.State.RandomState);
        Assert.Equal(expected.State.Units, actual.State.Units);
        Assert.Equal(expected.Events, actual.Events);
    }

    [Fact]
    public void 공격직전_저장복원은_피해와_사건을_중복시키지않는다()
    {
        var a = CombatUnit(1, 1, 0, RenewalTargetId.ForUnit(new UnitId(2)));
        var b = CombatUnit(2, 2, 1_000, RenewalTargetId.ForUnit(a.Id));
        var profiles = new[] { a, b }.ToDictionary(x => x.Id, Profile);
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.Attack, 50, [a, b],
            CombatProfiles: profiles, RandomState: 24680);
        var save = new RenewalMovementSaveService();
        var restored = save.Deserialize(save.Serialize(state));
        var simulator = new RenewalAdvanceSimulator(combat:
            new RenewalCombatPhaseService(new BalanceConfig(0)));

        var expected = simulator.StepPhase(state);
        var actual = simulator.StepPhase(restored);

        Assert.Equal(expected.State.CombatProfiles![a.Id].Participant.Pool,
            actual.State.CombatProfiles![a.Id].Participant.Pool);
        Assert.Equal(expected.State.CombatProfiles[b.Id].Participant.Pool,
            actual.State.CombatProfiles[b.Id].Participant.Pool);
        Assert.Equal(expected.Events.Count(x => x.Kind == RenewalAdvanceEventKind.AttackResolved),
            actual.Events.Count(x => x.Kind == RenewalAdvanceEventKind.AttackResolved));
    }

    private static RenewalUnitState CombatUnit(int id, int faction, long x,
        RenewalTargetId target) => RenewalUnitState.Create(new UnitId(id),
        new ContinuousPosition(x, 0), new ContinuousPosition(x, 0), 0) with
        {
            Owner = new FactionId(faction),
            Mode = RenewalOrderMode.Attack,
            AssignedTarget = target,
        };

    private static RenewalCombatProfile Profile(RenewalUnitState unit) => new(unit.Id,
        new BattleParticipant(new CombatStats(10_000, 10, 10), UnitMode.Attack,
            new TroopPool(10_000, 0)), BuildingAttack: 10);
}
