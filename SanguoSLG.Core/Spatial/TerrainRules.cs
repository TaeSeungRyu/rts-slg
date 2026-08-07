namespace SanguoSLG.Core.Spatial;

/// <summary>
/// 지형 통행 규칙(doc/spec-unit.md 통행 규칙 연결). 판정만 담당한다 —
/// 성곽 발자국처럼 지형이 아닌 점유 차단은 호출 쪽에서 겹쳐 건다.
/// </summary>
public static class TerrainRules
{
    /// <summary>해당 통행 영역의 유닛이 이 지형에 들어갈 수 있는가.</summary>
    public static bool CanEnter(MovementDomain domain, TerrainType terrain)
    {
        // 전 병종 공통 이동 불가: 돌산·기암·절벽·얼음 구조물, 그리고 항구 —
        // 항구는 성과 같은 점유 시설이라 걸어 들어가는 칸이 아니다(design-combat.md)
        if (terrain is TerrainType.RockMountain or TerrainType.Karst or TerrainType.Cliff
            or TerrainType.IceMountain or TerrainType.IceWallLarge or TerrainType.IceWallSmall
            or TerrainType.PortSmall)
        {
            return false;
        }

        // 소하천(River)은 타일 가장자리를 흐르는 강이라 육지 병종이 지난다(design-water.md).
        // 육지를 막는 물은 대하 계열뿐이다
        var deepWater = terrain is TerrainType.WaterShallow
            or TerrainType.WaterDeep or TerrainType.WaterRocks;

        // 소형산(TerrainType.Mountain)은 모든 육상 병종이 지난다(2026-08-07 사용자 정의).
        // 산악 통행 병종만 지나는 것은 산 지물(중간산 이상) — PassabilityMap이 건다
        return domain switch
        {
            // 배는 대하만 — 소하천(River)·암초(WaterRocks)는 지나지 못한다
            MovementDomain.DeepWater => terrain is TerrainType.WaterShallow or TerrainType.WaterDeep,
            _ => !deepWater,
        };
    }
}
