namespace SanguoSLG.Core.Spatial;

/// <summary>지물 종류별 점유 타일(발자국). doc/design-terrain.md의 배치 형태 정의를 따른다.</summary>
public static class FeatureFootprint
{
    private static readonly HexCoord[] MountainMedium = { new(0, 0), new(1, 0) };

    // 12시(앵커)·4시·8시로 붙은 삼각(원형) — 중간성과 같은 배치 형태.
    private static readonly HexCoord[] MountainLarge = { new(0, 0), new(0, 1), new(-1, 1) };

    /// <summary>앵커 기준 상대 오프셋.</summary>
    public static IReadOnlyList<HexCoord> OffsetsFor(FeatureType type) => type switch
    {
        FeatureType.MountainLarge => MountainLarge,
        _ => MountainMedium,
    };

    /// <summary>지물이 실제로 점유하는 타일들.</summary>
    public static IEnumerable<HexCoord> TilesFor(MapFeature feature)
    {
        foreach (var offset in OffsetsFor(feature.Type))
        {
            yield return feature.Position + offset;
        }
    }
}
