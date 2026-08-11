namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 한 부대의 전투 지속 상태(design-skill.md·design-stratagem.md). 선봉·부관의 액티브 게이지 두 개,
/// 모략력, 계략 숙달 포인트, 진행 중인 계략 예약을 묶는다. 하루-진행마다 갱신하고, 전투 시점에
/// "무엇이 발동하는가"(선봉 우선 액티브 1개·계략 발동/캔슬)를 정한다. 불변 값.
/// 발동한 액티브를 유형별(타격/방어/회복)로 BattleParticipant에 넣는 것은 상위(오케스트레이터)가 한다.
/// </summary>
public sealed record UnitCombatState(
    ActiveSkill? VanguardActive,
    ActiveGauge VanguardGauge,
    ActiveSkill? AdjutantActive,
    ActiveGauge AdjutantGauge,
    StratagemResource Resource,
    int MasteryPoints,
    StratagemReservation? Reservation)
{
    /// <summary>출전 상태로 생성한다(게이지 0, 모략력 가득, 예약 없음).</summary>
    public static UnitCombatState Create(int intellect, ActiveSkill? vanguardActive = null,
        ActiveSkill? adjutantActive = null, int masteryPoints = 0)
        => new(vanguardActive, new ActiveGauge(), adjutantActive, new ActiveGauge(),
            StratagemResource.FromIntellect(intellect), masteryPoints, null);

    /// <summary>계략 숙달 레벨(1~10).</summary>
    public int MasteryLevel => StratagemMastery.LevelFromPoints(MasteryPoints);

    /// <summary>야전 하루-진행 경과: 게이지·계략 예약을 <paramref name="days"/>만큼 진행한다.</summary>
    public UnitCombatState AdvanceField(int days) => this with
    {
        VanguardGauge = VanguardGauge.Tick(days),
        AdjutantGauge = AdjutantGauge.Tick(days),
        Reservation = Reservation?.Tick(days),
    };

    /// <summary>성 복귀: 게이지 0, 모략력 충전, 진행 중이던 계략 예약 취소.</summary>
    public UnitCombatState ReturnToCastle() => this with
    {
        VanguardGauge = new ActiveGauge(),
        AdjutantGauge = new ActiveGauge(),
        Resource = Resource.Refill(),
        Reservation = null,
    };

    /// <summary>명령 페이즈에서 계략을 예약한다(모략력·숙달 조건은 호출자가 확인).</summary>
    public UnitCombatState ReserveStratagem(Stratagem stratagem, UnitId targetId)
        => this with { Reservation = StratagemReservation.Reserve(stratagem, targetId) };

    /// <summary>
    /// 이 교전에서 발동할 액티브(선봉 우선, 준비된 것 1개)와 그 게이지를 소비한 새 상태.
    /// 없으면 (null, 그대로). 유형 분기는 호출자가 한다.
    /// </summary>
    public (ActiveSkill? Skill, UnitCombatState State) FiringActive()
    {
        if (VanguardActive is not null && VanguardGauge.IsReady)
        {
            return (VanguardActive, this with { VanguardGauge = VanguardGauge.Fire() });
        }
        if (AdjutantActive is not null && AdjutantGauge.IsReady)
        {
            return (AdjutantActive, this with { AdjutantGauge = AdjutantGauge.Fire() });
        }

        return (null, this);
    }

    /// <summary>진행 중인 계략의 이 시점 발동 판정(예약 없으면 Pending).</summary>
    public StratagemFireOutcome StratagemDue(bool targetValid)
        => Reservation is null ? StratagemFireOutcome.Pending : Reservation.Evaluate(targetValid);

    /// <summary>계략 발동: 모략력 소비, 숙달 +1, 예약 해제. 발동할 계략과 새 상태를 돌려준다.</summary>
    public (Stratagem Stratagem, UnitCombatState State) FireStratagem()
    {
        var stratagem = Reservation!.Stratagem;
        return (stratagem, this with
        {
            Resource = Resource.Spend(stratagem.Cost),
            MasteryPoints = MasteryPoints + 1,
            Reservation = null,
        });
    }

    /// <summary>계략 캔슬(대상 소실 등): 예약만 해제(페널티 없음).</summary>
    public UnitCombatState CancelStratagem() => this with { Reservation = null };
}
