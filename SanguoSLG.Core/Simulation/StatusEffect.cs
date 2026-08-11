namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 부대에 걸린 지속 상태(design-stratagem.md). 진행(AdvanceOrchestrator.Run)마다 1회 만분율
/// <see cref="TickBasisPoints"/>(강도 배율이 이미 곱해진 값)만큼 현재 병력에 피해를 주고,
/// <see cref="Remaining"/>이 0이 되면 만료된다. 정화는 <see cref="IsFire"/>로 화계/그 외를 가른다.
/// </summary>
/// <param name="Kind">상태 종류.</param>
/// <param name="TickBasisPoints">진행당 피해(만분율, 병력 대비). 강도 배율 반영 완료.</param>
/// <param name="Remaining">남은 진행 수.</param>
/// <param name="IsFire">화계 계열인가(정화 범위 판정용).</param>
public sealed record StatusEffect(StatusKind Kind, int TickBasisPoints, int Remaining, bool IsFire)
{
    /// <summary>남은 진행이 없으면 만료.</summary>
    public bool IsExpired => Remaining <= 0;

    /// <summary>현재 병력 기준 이번 진행 피해(내림).</summary>
    public int TickDamage(int troops) => (int)((long)troops * TickBasisPoints / 10000);

    /// <summary>한 진행 경과: 남은 진행 −1.</summary>
    public StatusEffect Tick() => this with { Remaining = Remaining - 1 };
}
