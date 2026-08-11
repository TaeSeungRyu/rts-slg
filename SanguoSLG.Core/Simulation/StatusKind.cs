namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 계략이 대상에 남기는 지속 상태의 종류(design-stratagem.md "계략 목록"). 지속 피해(DoT)와
/// 능력치 디버프를 담는다. 행동불가(혼란)·이동 결합(교란·수공 이동−1)은 후속.
/// </summary>
public enum StatusKind
{
    /// <summary>화상(화계). 지속 피해. 정화 = 소화.</summary>
    Burn,

    /// <summary>중독(독무). 지속 피해. 정화 = 진정(화계 외).</summary>
    Poison,

    /// <summary>공격 감소(수공). 대상의 준 피해를 일정 %만큼 줄인다.</summary>
    AttackDown,

    /// <summary>원거리 공격 감소(연막). 사거리 2 이상 부대의 준 피해를 줄인다.</summary>
    RangedDown,

    /// <summary>적성·패시브 무효(이간). 적성·가산 버킷을 중립(100)으로 되돌린다.</summary>
    Nullify,

    /// <summary>행동불가(혼란). 지속 동안 이동·공격·액티브를 못 한다(피격·방어는 정상).</summary>
    Daze,
}
