namespace SanguoSLG.Core.Spatial;

/// <summary>헥사 타일의 지형 종류. 지금은 표현(렌더)용이며, 통행/이동 비용 반영은 이후 단계.</summary>
public enum TerrainType
{
    Plains,
    Forest,
    Mountain,
    Desert,
    River,
    Bridge,

    /// <summary>대하(큰 강)의 얕은 물. 타일 전체가 물이다.</summary>
    WaterShallow,

    /// <summary>대하(큰 강)의 깊은 물. 깊이가 시각적으로 구분된다.</summary>
    WaterDeep,

    /// <summary>돌 모음 — 바위 무더기.</summary>
    Rocks,

    /// <summary>돌 모음2 — 큰 바위 언덕.</summary>
    RockHill,

    /// <summary>물에 있는 돌 — 물가/대하의 암초.</summary>
    WaterRocks,
}
