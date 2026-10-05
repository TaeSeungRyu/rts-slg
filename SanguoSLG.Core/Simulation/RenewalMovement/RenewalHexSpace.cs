namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Spatial;

/// <summary>flat-top axial 헥사와 Core 연속 평면 사이의 단일 변환 원본.</summary>
public static class RenewalHexSpace
{
    // sqrt(3) / 2를 1칸=1,000 정밀도에서 고정한다. 화면 좌표가 아닌 논리 평면이다.
    private const long AxialX = 867;
    private const long AxialHalfY = 500;

    public static ContinuousPosition Center(HexCoord hex) => new(
        checked(AxialX * hex.Q),
        checked(AxialHalfY * hex.Q + ContinuousPosition.UnitsPerTile * hex.R));

    /// <summary>가까운 axial 추정치 주변을 정수 거리로 비교하여 결정론적으로 타일을 고른다.</summary>
    public static HexCoord NearestHex(ContinuousPosition position)
    {
        var qEstimate = DivideRounded(position.X, AxialX);
        var rEstimate = DivideRounded(position.Y - AxialHalfY * qEstimate,
            ContinuousPosition.UnitsPerTile);
        var best = new HexCoord(checked((int)qEstimate), checked((int)rEstimate));
        var bestDistance = position.DistanceSquaredTo(Center(best));
        for (var dq = -2; dq <= 2; dq++)
        {
            for (var dr = -2; dr <= 2; dr++)
            {
                var candidate = new HexCoord(checked((int)qEstimate + dq), checked((int)rEstimate + dr));
                var distance = position.DistanceSquaredTo(Center(candidate));
                if (distance < bestDistance
                    || (distance == bestDistance && Compare(candidate, best) < 0))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
        }
        return best;
    }

    private static long DivideRounded(long value, long divisor) => value >= 0
        ? (value + divisor / 2) / divisor
        : (value - divisor / 2) / divisor;

    private static int Compare(HexCoord left, HexCoord right)
    {
        var q = left.Q.CompareTo(right.Q);
        return q != 0 ? q : left.R.CompareTo(right.R);
    }
}
