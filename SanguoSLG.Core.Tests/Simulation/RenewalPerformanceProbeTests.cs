namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalPerformanceProbeTests
{
    [Fact]
    public void StandardLoad_Measures50_100_300UnitsWithoutPerTickPathRecalculation()
    {
        var metrics = new RenewalPerformanceProbe().MeasureStandardLoad(3);

        Assert.Equal([50, 100, 300], metrics.Select(x => x.UnitCount));
        Assert.All(metrics, metric =>
        {
            Assert.Equal(metric.UnitCount * 3, metric.ProcessedUnitTicks);
            Assert.Equal(0, metric.PathRecalculations);
            Assert.True(metric.ElapsedMilliseconds >= 0);
            Assert.True(metric.AllocatedBytes >= 0);
        });
    }

    [Fact]
    public void ThirtyOverlappingFriendlies_CompleteTickWithinOperationalBudget()
    {
        var metric = new RenewalPerformanceProbe().Measure(30, 5);

        Assert.Equal(150, metric.ProcessedUnitTicks);
        Assert.True(metric.MillisecondsPerTick < 100,
            $"30부대 중첩 처리 예산 초과: {metric.MillisecondsPerTick:0.###}ms");
    }
}
