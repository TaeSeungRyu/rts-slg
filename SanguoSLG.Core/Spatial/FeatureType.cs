namespace SanguoSLG.Core.Spatial;

/// <summary>다중 타일 지형 지물의 종류.</summary>
public enum FeatureType
{
    /// <summary>중간산 — 2타일, 이동 불가 예정.</summary>
    MountainMedium,

    /// <summary>큰산 — 3타일(원형/삼각), 이동 불가 예정.</summary>
    MountainLarge,

    /// <summary>매우 큰산 — 5타일(중심+4방), 랜드마크 기암괴석. 이동 불가 예정.</summary>
    MountainHuge,

    /// <summary>폭포 절벽산 — 3타일(절벽 2 + 소 1), 이동 불가 예정.</summary>
    WaterfallCliff,

    /// <summary>중형 항구 — 2타일(동서 쌍), 남쪽이 물과 접해야 한다.</summary>
    PortMedium,
}
