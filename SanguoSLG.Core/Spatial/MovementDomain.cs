namespace SanguoSLG.Core.Spatial;

/// <summary>
/// 병종의 통행 영역(doc/spec-unit.md 지형 열). 병종 데이터(troop-types.json)가 생기면
/// 그쪽에서 값을 받는다 — land / land_mountain / deep_water.
/// </summary>
public enum MovementDomain
{
    /// <summary>육지 유닛 — 물·산악에 들어가지 못한다.</summary>
    Land,

    /// <summary>산악 통행 유닛(무당비군) — 육지 + 산악.</summary>
    LandMountain,

    /// <summary>대하 유닛(배) — 대하(깊은 물) 타일만.</summary>
    DeepWater,
}
