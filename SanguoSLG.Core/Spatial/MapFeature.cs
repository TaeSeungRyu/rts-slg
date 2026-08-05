namespace SanguoSLG.Core.Spatial;

/// <summary>
/// 다중 타일 지형 지물(중간산 등). 앵커 좌표와 종류를 가지며,
/// 점유 타일은 FeatureFootprint가 정의한다.
/// </summary>
public sealed record MapFeature(FeatureType Type, HexCoord Position);
