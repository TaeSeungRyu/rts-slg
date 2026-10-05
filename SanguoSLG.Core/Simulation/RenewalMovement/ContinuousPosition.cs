namespace SanguoSLG.Core.Simulation.RenewalMovement;

/// <summary>
/// Phase 18D 연속 이동의 엔진 독립 고정소수점 위치.
/// 인접 헥사 중심 거리 1칸을 <see cref="UnitsPerTile"/> 단위로 표현한다.
/// </summary>
public readonly record struct ContinuousPosition(long X, long Y)
{
    public const long UnitsPerTile = 1_000;

    public long DistanceSquaredTo(ContinuousPosition other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return checked(dx * dx + dy * dy);
    }
}
