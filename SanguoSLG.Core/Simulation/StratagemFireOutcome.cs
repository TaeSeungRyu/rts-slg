namespace SanguoSLG.Core.Simulation;

/// <summary>계략 예약의 발동 시점 판정 결과(design-stratagem.md).</summary>
public enum StratagemFireOutcome
{
    /// <summary>아직 발동일 전(대기).</summary>
    Pending,

    /// <summary>발동일에 대상이 유효 → 발동.</summary>
    Fired,

    /// <summary>발동일에 대상이 죽었거나 사거리·지형 조건을 벗어남 → 불발(페널티 없음).</summary>
    Cancelled,
}
