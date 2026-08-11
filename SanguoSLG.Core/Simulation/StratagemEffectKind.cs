namespace SanguoSLG.Core.Simulation;

/// <summary>계략 효과 종류(design-stratagem.md "계략 목록").</summary>
public enum StratagemEffectKind
{
    /// <summary>즉발 피해(낙뢰·폭파). BaseValue = 대상 병력 %.</summary>
    InstantDamage,

    /// <summary>지속 피해(화계·독무). BaseValue = 진행당 병력 %, Duration = 진행 수.</summary>
    DamageOverTime,

    /// <summary>디버프(혼란·이간·수공·교란·연막). BaseValue·Duration은 효과별 의미.</summary>
    Debuff,

    /// <summary>정화(소화·진정) — 아군 대상의 계략 효과 제거.</summary>
    Purge,
}
