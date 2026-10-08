namespace SanguoSLG.Core.Simulation.RenewalMovement;

public static class RenewalPlaybackSampler
{
    public static ContinuousPosition Sample(IReadOnlyList<ContinuousPosition> points,
        long elapsedMicroseconds, long durationMicroseconds)
    {
        if (points.Count == 0)
        {
            throw new ArgumentException("재생 위치가 비어 있습니다.", nameof(points));
        }
        if (points.Count == 1 || elapsedMicroseconds <= 0)
        {
            return points[0];
        }
        if (durationMicroseconds <= 0 || elapsedMicroseconds >= durationMicroseconds)
        {
            return points[^1];
        }

        var segments = points.Count - 1L;
        var scaled = checked(elapsedMicroseconds * segments);
        var left = (int)(scaled / durationMicroseconds);
        var remainder = scaled % durationMicroseconds;
        var right = Math.Min(left + 1, points.Count - 1);
        return new ContinuousPosition(
            points[left].X + checked((points[right].X - points[left].X) * remainder)
                / durationMicroseconds,
            points[left].Y + checked((points[right].Y - points[left].Y) * remainder)
                / durationMicroseconds);
    }
}
