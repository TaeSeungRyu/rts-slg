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
    StratagemReservation? Reservation,
    IReadOnlyList<StatusEffect> Statuses)
{
    /// <summary>출전 상태로 생성한다(게이지 0, 모략력 가득, 예약 없음, 상태 없음).</summary>
    public static UnitCombatState Create(int intellect, ActiveSkill? vanguardActive = null,
        ActiveSkill? adjutantActive = null, int masteryPoints = 0)
        => new(vanguardActive, new ActiveGauge(), adjutantActive, new ActiveGauge(),
            StratagemResource.FromIntellect(intellect), masteryPoints, null, []);

    /// <summary>계략 숙달 레벨(1~10).</summary>
    public int MasteryLevel => StratagemMastery.LevelFromPoints(MasteryPoints);

    /// <summary>야전 하루-진행 경과: 게이지·계략 예약을 <paramref name="days"/>만큼 진행한다.</summary>
    public UnitCombatState AdvanceField(int days) => this with
    {
        VanguardGauge = VanguardGauge.Tick(days),
        AdjutantGauge = AdjutantGauge.Tick(days),
        Reservation = Reservation?.Tick(days),
    };

    /// <summary>이동 경과: 도시/부대 계략 예약만 진행하고 액티브 게이지는 충전하지 않는다.</summary>
    public UnitCombatState AdvanceTravel(int days) => this with
    {
        Reservation = Reservation?.Tick(days),
    };

    /// <summary>실제 교전 참여: 공격 또는 피격에 참여한 진행마다 액티브 게이지를 1칸 충전한다.</summary>
    public UnitCombatState AdvanceCombat() => this with
    {
        VanguardGauge = VanguardGauge.Tick(1),
        AdjutantGauge = AdjutantGauge.Tick(1),
    };

    /// <summary>성 복귀: 게이지 0, 모략력 충전, 계략 예약 취소, 걸린 지속 상태 해제.</summary>
    public UnitCombatState ReturnToCastle() => this with
    {
        VanguardGauge = new ActiveGauge(),
        AdjutantGauge = new ActiveGauge(),
        Resource = Resource.Refill(),
        Reservation = null,
        Statuses = [],
    };

    /// <summary>지속 상태를 건다. 같은 종류는 새 것으로 대체(갱신) — 중첩하지 않는다.</summary>
    public UnitCombatState AddStatus(StatusEffect status)
    {
        var kept = Statuses.Where(s => s.Kind != status.Kind).ToList();
        kept.Add(status);
        return this with { Statuses = kept };
    }

    /// <summary>현재 병력 기준 이번 진행의 지속 피해 합(모든 상태 tick).</summary>
    public int TotalTickDamage(int troops) => Statuses.Sum(s => s.TickDamage(troops));

    /// <summary>한 진행 경과: 모든 상태의 남은 진행을 줄이고 만료된 것을 뗀다.</summary>
    public UnitCombatState TickStatuses()
        => this with { Statuses = Statuses.Select(s => s.Tick()).Where(s => !s.IsExpired).ToList() };

    /// <summary>정화: 범위에 해당하는 지속 상태를 제거한다. 진정은 화계와 그 밖의 해로운 상태를 연속 정화한다.</summary>
    public UnitCombatState Purge(PurgeScope scope) => scope switch
    {
        PurgeScope.Fire => this with { Statuses = Statuses.Where(s => !s.IsFire).ToList() },
        PurgeScope.NonFire => this with { Statuses = Statuses.Where(s => s.IsFire || s.Kind == StatusKind.Evasion).ToList() },
        _ => this,
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
        // Phase 14A 준비 단계: 책략형 액티브는 사용자가 확정 효과를 다시 정의하기 전까지
        // 게이지를 소비하거나 "발동"으로 보고하지 않는다. 기존 저장/장수 배정은 보존한다.
        if (VanguardActive is { Type: not ActiveType.Tactic } && VanguardGauge.IsReady)
        {
            return (VanguardActive, this with { VanguardGauge = VanguardGauge.Fire() });
        }
        if (AdjutantActive is { Type: not ActiveType.Tactic } && AdjutantGauge.IsReady)
        {
            return (AdjutantActive, this with { AdjutantGauge = AdjutantGauge.Fire() });
        }

        return (null, this);
    }

    /// <summary>
    /// 준비된 최우선 액티브가 방어형일 때만 소비한다. 주장 우선 규칙을 건너뛰지 않으므로
    /// 주장 공격형이 준비된 상태에서 부관 방어형이 먼저 발동하지 않는다.
    /// </summary>
    public (ActiveSkill? Skill, UnitCombatState State) FiringDefenseActive()
    {
        if (VanguardActive is { } v && VanguardGauge.IsReady)
            return v.Type == ActiveType.Defense
                ? (v, this with { VanguardGauge = VanguardGauge.Fire() })
                : (null, this);
        if (AdjutantActive is { } a && AdjutantGauge.IsReady)
            return a.Type == ActiveType.Defense
                ? (a, this with { AdjutantGauge = AdjutantGauge.Fire() })
                : (null, this);
        return (null, this);
    }

    /// <summary>성·항구 공격에서 준비된 건물 전용 타격 액티브만 소비한다.</summary>
    public (ActiveSkill? Skill, UnitCombatState State) FiringBuildingActive()
    {
        if (VanguardActive is { Type: ActiveType.Strike, BuildingOnly: true } && VanguardGauge.IsReady)
            return (VanguardActive, this with { VanguardGauge = VanguardGauge.Fire() });
        if (AdjutantActive is { Type: ActiveType.Strike, BuildingOnly: true } && AdjutantGauge.IsReady)
            return (AdjutantActive, this with { AdjutantGauge = AdjutantGauge.Fire() });
        return (null, this);
    }

    /// <summary>5일 충전된 책략형 액티브만 선봉 우선으로 소비한다. 유효 대상이 없을 때는 호출하지 않는다.</summary>
    public (ActiveSkill? Skill, UnitCombatState State) FiringTactic()
    {
        if (VanguardActive is { Type: ActiveType.Tactic } && VanguardGauge.IsReady)
        {
            return (VanguardActive, this with { VanguardGauge = VanguardGauge.Fire() });
        }
        if (AdjutantActive is { Type: ActiveType.Tactic } && AdjutantGauge.IsReady)
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
