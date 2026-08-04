namespace SanguoSLG.Core.Domain;

using SanguoSLG.Core.Spatial;

/// <summary>
/// 성곽 등급별 점유 타일(발자국). 사용자 정의(2026-08-04):
/// 중간성 = 육각 3개가 12시·4시·8시 방향으로 서로 붙은 모양(앵커=12시 타일),
/// 큰성 = 위 2개 + 아래 3개(앵커=윗줄 왼쪽 타일).
/// </summary>
public static class CastleFootprint
{
    private static readonly HexCoord[] Small = { new(0, 0) };

    // 12시(앵커), 4시, 8시 — 세 육각이 한 꼭짓점을 공유하며 붙는다.
    private static readonly HexCoord[] Medium = { new(0, 0), new(0, 1), new(-1, 1) };

    // 윗줄 2개(앵커, 동쪽) + 아랫줄 3개.
    private static readonly HexCoord[] Large = { new(0, 0), new(1, 0), new(-1, 1), new(0, 1), new(1, 1) };

    /// <summary>앵커(도시 위치) 기준 상대 오프셋.</summary>
    public static IReadOnlyList<HexCoord> OffsetsFor(CastleSize size) => size switch
    {
        CastleSize.Medium => Medium,
        CastleSize.Large => Large,
        _ => Small,
    };

    /// <summary>도시가 실제로 점유하는 타일들(앵커 + 오프셋).</summary>
    public static IEnumerable<HexCoord> TilesFor(City city)
    {
        foreach (var offset in OffsetsFor(city.Castle))
        {
            yield return city.Position + offset;
        }
    }
}
