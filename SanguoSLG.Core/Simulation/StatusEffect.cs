namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 부대에 걸린 지속 상태(design-stratagem.md). 지속 피해(DoT)는 진행(AdvanceOrchestrator.Run)마다
/// 1회 만분율 <see cref="TickBasisPoints"/>(강도 반영)만큼 현재 병력에 피해를 준다. 능력치 디버프는
/// 전투 산출에 반영된다 — <see cref="AtkDownPercent"/>(수공·연막의 준 피해 감소), <see cref="NullifyAptPassive"/>
/// (이간의 적성·패시브 무효). <see cref="Remaining"/>이 0이 되면 만료. 정화는 <see cref="IsFire"/>로
/// 화계/그 외를 가른다(소화=화계, 진정=화계 외).
/// </summary>
/// <param name="Kind">상태 종류.</param>
/// <param name="TickBasisPoints">지속 피해(만분율, 병력 대비). 디버프는 0.</param>
/// <param name="Remaining">남은 진행 수.</param>
/// <param name="IsFire">화계 계열인가(정화 범위 판정용).</param>
/// <param name="AtkDownPercent">준 피해 감소 %(수공·연막). 0이면 없음.</param>
/// <param name="RangedOnly">사거리 2 이상 부대에만 적용(연막).</param>
/// <param name="NullifyAptPassive">적성·가산 버킷을 100으로 되돌림(이간).</param>
/// <param name="MoveDownTiles">이동 속도 감소 칸(수공). 0이면 없음.</param>
public sealed record StatusEffect(
    StatusKind Kind,
    int TickBasisPoints,
    int Remaining,
    bool IsFire,
    int AtkDownPercent = 0,
    bool RangedOnly = false,
    bool NullifyAptPassive = false,
    int MoveDownTiles = 0)
{
    /// <summary>행동불가(혼란) — 이동·공격·액티브 금지.</summary>
    public bool IsDaze => Kind == StatusKind.Daze;

    /// <summary>남은 진행이 없으면 만료.</summary>
    public bool IsExpired => Remaining <= 0;

    /// <summary>현재 병력 기준 이번 진행 피해(내림). 디버프는 0.</summary>
    public int TickDamage(int troops) => (int)((long)troops * TickBasisPoints / 10000);

    /// <summary>한 진행 경과: 남은 진행 −1.</summary>
    public StatusEffect Tick() => this with { Remaining = Remaining - 1 };
}
