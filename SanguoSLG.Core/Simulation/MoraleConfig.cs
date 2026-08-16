namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 사기·훈련 밸런스(design-unit-state 2·3단계). 사기·훈련도는 전투 공/방에 배수로 작용하고,
/// 사기는 진행마다 전투 성과로 오르내리며 임계 밑이면 패주한다. 값은 data/morale.json에서 주입.
/// </summary>
public sealed record MoraleConfig
{
    /// <summary>사기 공/방 보너스% = (사기 − 50) × num ÷ den. 기본 2/5 → 사기 100 = +20%, 0 = −20%.</summary>
    public int MoraleBonusNum { get; init; } = 2;
    public int MoraleBonusDen { get; init; } = 5;

    /// <summary>훈련도 공/방 보너스% = (훈련 − 50) × num ÷ den. 기본 1/5 → 훈련 100 = +10%.</summary>
    public int TrainingBonusNum { get; init; } = 1;
    public int TrainingBonusDen { get; init; } = 5;

    /// <summary>패주 진입 임계(이 밑이면 패주)와 해제 임계(이 이상이면 복귀) — 히스테리시스.</summary>
    public int RoutThreshold { get; init; } = 20;
    public int RoutRecover { get; init; } = 40;

    /// <summary>사기 증감(진행당): 교전 우세, 적 격파, 굶주림, 무전투 휴식.</summary>
    public int WinGain { get; init; } = 5;
    public int KillGain { get; init; } = 10;
    public int StarveLoss { get; init; } = 10;
    public int RestGain { get; init; } = 2;

    /// <summary>피해 사기 하락 = 그 진행 피해율% × num ÷ den. 기본 1/2 → 10% 손실에 −5.</summary>
    public int DamageLossNum { get; init; } = 1;
    public int DamageLossDen { get; init; } = 2;
}
