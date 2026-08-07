namespace SanguoSLG.Core.Spatial;

using SanguoSLG.Core.Domain;

/// <summary>
/// 병종 통행 판정의 조립점(2026-08-07 사용자 정의):
/// 성·항구는 어떤 병종도 못 들어가고, 산 지물(중간산 이상)은 산악 통행 병종만,
/// 소형산(지형)은 모두, 대하는 배만·배는 대하만, 얼음 지형은 전면 불가.
/// 맵 경계·지형 규칙(TerrainRules) 위에 점유 시설과 산 지물을 겹쳐 건다.
/// </summary>
public sealed class PassabilityMap
{
    private readonly HexMap _map;
    private readonly HashSet<HexCoord> _blockedForAll = new();
    private readonly HashSet<HexCoord> _mountainOnly = new();

    public PassabilityMap(HexMap map, IEnumerable<MapFeature> features, IEnumerable<City> cities)
    {
        _map = map;

        foreach (var city in cities)
        {
            foreach (var tile in CastleFootprint.TilesFor(city))
            {
                _blockedForAll.Add(tile);
            }
        }

        foreach (var feature in features)
        {
            // 중형 항구는 성과 같은 점유 시설, 폭포 절벽산은 절벽이라 전면 불가.
            // 나머지 지물(중간산·큰산·매우 큰산)은 산악 통행 병종만 지난다
            var target = feature.Type is FeatureType.PortMedium or FeatureType.WaterfallCliff
                ? _blockedForAll
                : _mountainOnly;
            foreach (var tile in FeatureFootprint.TilesFor(feature))
            {
                target.Add(tile);
            }
        }
    }

    /// <summary>해당 통행 영역의 유닛이 이 좌표에 들어갈 수 있는가.</summary>
    public bool CanEnter(MovementDomain domain, HexCoord coord)
    {
        if (!_map.Contains(coord) || _blockedForAll.Contains(coord))
        {
            return false;
        }

        if (_mountainOnly.Contains(coord))
        {
            return domain == MovementDomain.LandMountain;
        }

        return TerrainRules.CanEnter(domain, _map.TerrainAt(coord));
    }
}
