namespace SanguoSLG.Core.Simulation;

/// <summary>전투 액티브 스킬 유형(design-skill-actives.md). 계략은 별도 시스템이다.</summary>
public enum ActiveType
{
    /// <summary>타격형 — 일반 공격을 특수 공격으로 대체(무력 연동).</summary>
    Strike,

    /// <summary>방어형 — 일반 공격은 유지하고 받는 피해를 줄인다(무력 연동).</summary>
    Defense,

    /// <summary>회복형 — 일반 공격은 유지하고 병력을 회복한다(지력 연동).</summary>
    Heal,
}
