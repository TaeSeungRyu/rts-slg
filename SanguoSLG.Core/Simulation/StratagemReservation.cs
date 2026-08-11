namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 계략 예약(design-stratagem.md "시전·판정"). 명령 페이즈에서 시전하면 <see cref="LeadDays"/>(2일)
/// 뒤에 발동한다 — 시전 당일 1일차, 3일차 발동. 야전 경과일이 쌓여 남은 일수가 0 이하가 되면
/// 발동일이고, 그 시점에 대상 유효성으로 발동/캔슬을 가른다. 캔슬은 페널티 없음(모략력 미차감,
/// 발동일 공격 불가 없음). 불변 값.
/// </summary>
/// <param name="Stratagem">시전한 계략.</param>
/// <param name="TargetId">대상 부대.</param>
/// <param name="DaysUntilFire">발동까지 남은 일수.</param>
public sealed record StratagemReservation(Stratagem Stratagem, UnitId TargetId, int DaysUntilFire)
{
    /// <summary>시전~발동 지연(design 확정 2일).</summary>
    public const int LeadDays = 2;

    /// <summary>명령 페이즈에서 계략을 예약한다(발동까지 2일).</summary>
    public static StratagemReservation Reserve(Stratagem stratagem, UnitId targetId)
        => new(stratagem, targetId, LeadDays);

    /// <summary>발동일에 도달했는가(남은 일수 0 이하).</summary>
    public bool IsDue => DaysUntilFire <= 0;

    /// <summary>야전 하루-진행 경과: 남은 일수를 <paramref name="days"/>만큼 줄인다.</summary>
    public StratagemReservation Tick(int days) => days <= 0 ? this : this with { DaysUntilFire = DaysUntilFire - days };

    /// <summary>
    /// 이 시점의 발동/캔슬 판정. <paramref name="targetValid"/>는 발동일에 대상이 살아 있고 사거리·지형
    /// 조건을 만족하는가(상위가 계산해 넘긴다).
    /// </summary>
    public StratagemFireOutcome Evaluate(bool targetValid)
        => !IsDue ? StratagemFireOutcome.Pending
            : targetValid ? StratagemFireOutcome.Fired
            : StratagemFireOutcome.Cancelled;
}
