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

    /// <summary>논 — 물 댄 벼농사 타일.</summary>
    Paddy,

    /// <summary>밭 — 밭농사 타일.</summary>
    Farm,

    /// <summary>공방 — 생산 시설 타일.</summary>
    Workshop,

    /// <summary>돌산 — 돌 바위 산(1타일, 이동 불가 예정).</summary>
    RockMountain,

    /// <summary>기암 소석림 — 매우 큰산(기암 기둥 숲)과 어울리는 1타일 기둥 바위(이동 불가 예정).</summary>
    Karst,

    /// <summary>작은 절벽 — 폭포 절벽산과 어울리는 1타일 단애(이동 불가 예정).</summary>
    Cliff,

    /// <summary>얼음산 — 빙설 첨탑 산(1타일, 이동 불가 예정).</summary>
    IceMountain,

    /// <summary>거대한 얼음벽 — 타일을 가로지르는 높은 빙벽(1타일, 이동 불가 예정).</summary>
    IceWallLarge,

    /// <summary>작은 얼음벽 모음 — 부서진 낮은 빙벽 조각들(1타일, 이동 불가 예정).</summary>
    IceWallSmall,
}
