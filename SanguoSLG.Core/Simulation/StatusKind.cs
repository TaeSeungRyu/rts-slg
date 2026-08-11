namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 계략이 대상에 남기는 지속 상태의 종류(design-stratagem.md "계략 목록"). 이번 증분은 지속
/// 피해(DoT)만 — 화계는 <see cref="Burn"/>, 독무는 <see cref="Poison"/>. 능력치 디버프·행동불가는 후속.
/// </summary>
public enum StatusKind
{
    /// <summary>화상(화계). 정화 = 소화.</summary>
    Burn,

    /// <summary>중독(독무). 정화 = 진정(화계 외).</summary>
    Poison,
}
