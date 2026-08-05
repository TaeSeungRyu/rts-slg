namespace SanguoSLG.Core.Spatial;

/// <summary>지물 종류별 점유 타일(발자국). doc/design-terrain.md의 배치 형태 정의를 따른다.</summary>
public static class FeatureFootprint
{
    private static readonly HexCoord[] MountainMedium = { new(0, 0), new(1, 0) };

    // 12시(앵커)·4시·8시로 붙은 삼각(원형) — 중간성과 같은 배치 형태.
    private static readonly HexCoord[] MountainLarge = { new(0, 0), new(0, 1), new(-1, 1) };

    // 중심(앵커) + 동·서·남서·남동 — 둥근 랜드마크 산괴.
    private static readonly HexCoord[] MountainHuge =
        { new(0, 0), new(1, 0), new(-1, 0), new(-1, 1), new(0, 1) };

    // 절벽 2타일(앵커·동쪽) + 남쪽 소(웅덩이) 1타일.
    private static readonly HexCoord[] WaterfallCliff = { new(0, 0), new(1, 0), new(0, 1) };

    // 앵커·동쪽 2타일 쌍 — 남쪽이 물과 접하는 자리에 배치한다.
    private static readonly HexCoord[] PortMedium = { new(0, 0), new(1, 0) };

    /// <summary>앵커 기준 상대 오프셋.</summary>
    public static IReadOnlyList<HexCoord> OffsetsFor(FeatureType type) => type switch
    {
        FeatureType.MountainLarge => MountainLarge,
        FeatureType.MountainHuge => MountainHuge,
        FeatureType.WaterfallCliff => WaterfallCliff,
        FeatureType.PortMedium => PortMedium,
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
