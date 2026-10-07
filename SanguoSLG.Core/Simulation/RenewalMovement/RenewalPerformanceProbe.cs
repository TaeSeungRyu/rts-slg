namespace SanguoSLG.Core.Simulation.RenewalMovement;

using System.Diagnostics;
using SanguoSLG.Core.Domain;

public sealed record RenewalLoadMetric(int UnitCount, int Ticks,
    double ElapsedMilliseconds, long AllocatedBytes, int ProcessedUnitTicks,
    int PathRecalculations)
{
    public double MillisecondsPerTick => Ticks == 0 ? 0 : ElapsedMilliseconds / Ticks;
}

/// <summary>Phase 18D 전용 50/100/300부대 고정 입력 부하 측정기.</summary>
public sealed class RenewalPerformanceProbe
{
    public RenewalLoadMetric Measure(int unitCount, int ticks = 10)
    {
        if (unitCount <= 0) throw new ArgumentOutOfRangeException(nameof(unitCount));
        if (ticks <= 0 || ticks >= RenewalAdvanceSimulator.MovementTicksPerDay)
            throw new ArgumentOutOfRangeException(nameof(ticks));

        var units = Enumerable.Range(1, unitCount).Select(index =>
            RenewalUnitState.Create(new UnitId(index),
                new ContinuousPosition((index % 20) * 500L, (index / 20) * 500L),
                new ContinuousPosition(20000 + (index % 20) * 500L, (index / 20) * 500L), 3)
            with { Owner = new FactionId(1) }).ToList();
        var simulator = new RenewalAdvanceSimulator();
        var state = simulator.Start(units);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < ticks; index++)
        {
            state = simulator.StepMovementTick(state).State;
        }
        stopwatch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        return new RenewalLoadMetric(unitCount, ticks, stopwatch.Elapsed.TotalMilliseconds,
            allocated, checked(unitCount * ticks), 0);
    }

    public IReadOnlyList<RenewalLoadMetric> MeasureStandardLoad(int ticks = 10) =>
        [Measure(50, ticks), Measure(100, ticks), Measure(300, ticks)];
}
