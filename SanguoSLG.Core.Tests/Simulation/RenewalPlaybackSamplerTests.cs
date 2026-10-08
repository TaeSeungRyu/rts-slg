namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class RenewalPlaybackSamplerTests
{
    [Fact]
    public void 재생률과_무관하게_종료위치는_마지막_연속좌표다()
    {
        IReadOnlyList<ContinuousPosition> points = Enumerable.Range(0, 51)
            .Select(index => new ContinuousPosition(index * 20, index * 10)).ToList();
        const long duration = 1_500_000;

        foreach (var fps in new[] { 30, 60, 144 })
        {
            var elapsed = 0L;
            var frame = 1_000_000L / fps;
            while (elapsed < duration) { elapsed += frame; }
            Assert.Equal(points[^1], RenewalPlaybackSampler.Sample(points, elapsed, duration));
        }
    }

    [Fact]
    public void 틱사이는_연속좌표로_보간된다()
    {
        var points = new[]
        {
            new ContinuousPosition(0, 0),
            new ContinuousPosition(100, 50),
            new ContinuousPosition(200, 100),
        };

        var sampled = RenewalPlaybackSampler.Sample(points, 375_000, 1_500_000);

        Assert.Equal(new ContinuousPosition(50, 25), sampled);
    }
}
