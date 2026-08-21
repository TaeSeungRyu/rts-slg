namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 훈련도 밸런스(design-unit-state 3단계). 훈련도는 전투 공/방에 배수로 작용한다.
/// 사기 시스템은 2026-08-21 전면 폐지(사용자 결정) — 부대 질 보정은 훈련도만 남는다.
/// </summary>
public sealed record TrainingConfig
{
    /// <summary>훈련도 공/방 보너스% = (훈련 − 50) × num ÷ den. 기본 1/5 → 훈련 100 = +10%.</summary>
    public int TrainingBonusNum { get; init; } = 1;
    public int TrainingBonusDen { get; init; } = 5;
}
