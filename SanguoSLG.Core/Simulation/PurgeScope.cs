namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 정화 계략이 제거하는 상태 범위(design-stratagem.md "정화 계략"). 소화는 화계(<see cref="Fire"/>),
/// 진정은 화계 이외(<see cref="NonFire"/>)를 제거한다. 비정화 계략은 <see cref="None"/>.
/// </summary>
public enum PurgeScope
{
    /// <summary>정화 아님.</summary>
    None,

    /// <summary>화계(Burn) 상태만 제거 — 소화.</summary>
    Fire,

    /// <summary>화계 이외의 상태 제거 — 진정.</summary>
    NonFire,
}
