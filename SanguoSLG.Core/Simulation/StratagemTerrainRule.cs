namespace SanguoSLG.Core.Simulation;

/// <summary>계략 발동 지형 조건(design-stratagem.md "발동 지역"). 판정 기준은 대상 타일.</summary>
public enum StratagemTerrainRule
{
    /// <summary>지형 제한 없음.</summary>
    None,

    /// <summary>대상이 소하천일 때만(수공).</summary>
    RiverOnly,

    /// <summary>대상이 소하천이면 불가(화계).</summary>
    RiverForbidden,
}
