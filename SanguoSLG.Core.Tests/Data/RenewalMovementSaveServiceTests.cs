namespace SanguoSLG.Core.Tests.Data;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalMovementSaveServiceTests
{
    [Fact]
    public void RoundTrip_PreservesOverlapPursuitSiegeWaitAndRandomState()
    {
        var target = new RenewalTargetId(RenewalTargetKind.Site, 77);
        var units = new[]
        {
            RenewalUnitState.Create(new UnitId(1), new(100, 200), new(500, 200), 3) with
            {
                Owner = new FactionId(1), Mode = RenewalOrderMode.Advance,
                PursuitTarget = target, CommandId = 11,
            },
            RenewalUnitState.Create(new UnitId(2), new(100, 200), new(500, 200), 3) with
            {
                Owner = new FactionId(1), Mode = RenewalOrderMode.Attack,
                AssignedTarget = target, StopReason = RenewalStopReason.SiegeCapacity,
                AttackRangeReachedTick = 17, CommandId = 12,
            },
        };
        var state = new RenewalAdvanceState(4, RenewalAdvancePhase.Attack, 50, units,
            ExternalTargets: [new(target, new FactionId(2), new(500, 200))],
            RandomState: 987654321);
        var service = new RenewalMovementSaveService();

        var loaded = service.Deserialize(service.Serialize(state));

        Assert.Equal(state.Day, loaded.Day);
        Assert.Equal(state.Phase, loaded.Phase);
        Assert.Equal(987654321, loaded.RandomState);
        Assert.Equal(2, loaded.Units.Count);
        Assert.Equal(loaded.Units[0].Position, loaded.Units[1].Position);
        Assert.Equal(target, loaded.Units[0].PursuitTarget);
        Assert.Equal(RenewalStopReason.SiegeCapacity, loaded.Units[1].StopReason);
        Assert.Equal(17, loaded.Units[1].AttackRangeReachedTick);
    }

    [Fact]
    public void Deserialize_RejectsLegacyOrDifferentMode()
    {
        var service = new RenewalMovementSaveService();
        var error = Assert.Throws<InvalidDataException>(() => service.Deserialize(
            "{\"SchemaVersion\":1,\"Mode\":\"campaign\",\"State\":null}"));
        Assert.Contains("기존 캠페인", error.Message);
    }

    [Fact]
    public void RoundTrip_ContinuesDeterministically()
    {
        var simulator = new RenewalAdvanceSimulator();
        var initial = simulator.Start([
            RenewalUnitState.Create(new UnitId(8), new(0, 0), new(1000, 0), 2),
        ]) with { RandomState = 42 };
        var after = simulator.StepMovementTick(initial).State;
        var service = new RenewalMovementSaveService();
        var loaded = service.Deserialize(service.Serialize(after));

        var expected = simulator.StepMovementTick(after).State;
        var actual = simulator.StepMovementTick(loaded).State;

        Assert.Equal(expected.Day, actual.Day);
        Assert.Equal(expected.Phase, actual.Phase);
        Assert.Equal(expected.MovementTick, actual.MovementTick);
        Assert.Equal(expected.Units[0], actual.Units[0]);
        Assert.Equal(expected.RandomState, actual.RandomState);
    }
}
