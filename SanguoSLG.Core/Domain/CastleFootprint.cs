namespace SanguoSLG.Core.Domain;

using SanguoSLG.Core.Spatial;

/// <summary>
/// 성곽 등급별 점유 타일(발자국). doc/design-terrain.md의 배치 형태 정의를 따른다:
/// 1타일=단일, 3타일=삼각(원형), 5타일=중심+꽃잎(원형). 3타일부터 직선이 아닌 원형 모양.
/// </summary>
public static class CastleFootprint
{
    private static readonly HexCoord[] Small = { new(0, 0) };
    private static readonly HexCoord[] Medium = { new(0, 0), new(1, 0), new(0, 1) };
    private static readonly HexCoord[] Large = { new(0, 0), new(1, 0), new(0, 1), new(-1, 1), new(1, -1) };

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
