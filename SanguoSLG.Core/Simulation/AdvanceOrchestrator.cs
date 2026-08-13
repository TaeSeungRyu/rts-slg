namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 한 "진행"을 이동 → 계략 발동 → 전투 페이즈 → 정산으로 묶는다(design-combat.md "전투 페이즈 발동"
/// 순환). 이동 시뮬을 돌려 정지시킨 뒤, 경과일만큼 발동 상태를 진행하고, 예약된 계략을 발동하며
/// (발동일엔 시전 부대 공격 불가), 사거리 전수검사로 교전을 만들어 액티브 발동(선봉 우선)을 얹어
/// 동시 정산한다. 결과로 위치·병력·발동 상태가 갱신된 부대를 돌려준다. 지속 상태(DoT·능력치
/// 디버프·행동불가), 정화, 강제 후퇴(교란)까지 반영하고, 병력 0(전멸) 부대는 결과에서 뺀다(소멸 —
/// Game이 영혼 상승 연출로 처리). 성 복귀 감지는 후속.
/// </summary>
public sealed class AdvanceOrchestrator
{
    private readonly MovementSimulator _movement;
    private readonly CombatPhaseResolver _combat;
    private readonly int _woundedPercent;
    private readonly Func<HexCoord, TerrainType> _terrainAt;

    public AdvanceOrchestrator(
        MovementSimulator movement,
        CombatPhaseResolver combat,
        int woundedPercent = 70,
        Func<HexCoord, TerrainType>? terrainAt = null)
    {
        _movement = movement;
        _combat = combat;
        _woundedPercent = woundedPercent;
        _terrainAt = terrainAt ?? (_ => TerrainType.Plains);
    }

    public AdvanceTurn Run(IReadOnlyList<CombatUnit> units, int maxDays = 7,
        IReadOnlyList<SiegeSite>? castles = null)
    {
        // 병력 0(전멸) 부대는 진행에서 빠진다 — 이동·전투·점유·표적에서 모두 제외한다.
        units = units.Where(u => u.Pool.Active > 0).ToList();

        // 진행 시작 시점에 행동불가(혼란)인 부대 — 이동·전투 모두 이 스냅샷으로 판정한다
        // (상태 tick이 진행 중간에 남은 진행을 줄여도, 그 진행의 효과는 온전히 적용).
        var dazedAtStart = units.Where(IsDazed).Select(u => u.Id).ToHashSet();

        // 1) 이동 — 진행 정지까지. 걸린 상태(혼란=행동불가, 수공=이동−1)를 이동 입력에 반영한다.
        var move = _movement.Advance(units.Select(MovementField).ToList(), maxDays, castles);
        var moved = move.Units.ToDictionary(f => f.Id);

        // 2) 위치만 갱신(임시 이동 스탯은 버림) + 경과일만큼 발동 상태 진행(야전 가정).
        var state = new Dictionary<UnitId, CombatUnit>();
        foreach (var u in units)
        {
            state[u.Id] = u with
            {
                Field = u.Field with { Position = moved[u.Id].Position },
                State = u.State.AdvanceField(move.Days),
            };
        }

        // 2.5) 지속 상태(DoT) 틱 — 진행당 1회. 서 있는 화상·독이 병력을 깎고 남은 진행이 준다.
        //      새로 걸리는 상태(4단계)는 이번 진행엔 tick하지 않는다(다음 진행부터).
        var statusDamage = new Dictionary<UnitId, int>();
        foreach (var id in state.Keys.ToList())
        {
            var u = state[id];
            if (u.State.Statuses.Count == 0)
            {
                continue;
            }

            var tick = u.State.TotalTickDamage(u.Pool.Active);
            var ticked = u.State.TickStatuses();
            var pool = tick > 0 ? u.Pool.TakeDamage(tick, _woundedPercent) : u.Pool;
            state[id] = u with { Pool = pool, State = ticked };
            if (tick > 0)
            {
                statusDamage[id] = tick;
            }
        }

        // 3) 계략 발동 — 예약이 발동일에 도달하면 대상 유효성으로 발동/캔슬. 즉발·지속 피해, 디버프,
        //    정화, 강제 후퇴(교란)를 여기서 적용한다. 발동 부대는 이번 교전 공격을 하지 않는다.
        //    후퇴가 위치를 바꾸므로 교전 탐지보다 먼저 발동한다.
        var stratagemDamage = new Dictionary<UnitId, int>();
        var firedStratagems = FireStratagems(state, stratagemDamage);

        // 4) 전투 페이즈 발동 — 정지·후퇴가 반영된 위치로 사거리 전수검사. 발동 부대와 행동불가(혼란)
        //    부대는 공격자에서 뺀다(피격·방어는 정상).
        var engagements = CombatPhase.DetectEngagements(state.Values.Select(u => u.Field).ToList())
            .Where(e => !firedStratagems.ContainsKey(e.Attacker)
                && !(dazedAtStart.Contains(e.Attacker) || IsDazed(state[e.Attacker])))
            .ToList();

        if (engagements.Count == 0)
        {
            return new AdvanceTurn(Ordered(state), move, null, NoActives, firedStratagems, statusDamage, stratagemDamage);
        }

        var attackers = engagements.Select(e => e.Attacker).ToHashSet();
        var participating = engagements
            .SelectMany(e => e.Targets.Append(e.Attacker))
            .ToHashSet();

        // 4) 교전 참가 부대마다 액티브 발동(선봉 우선)을 정하고 BattleParticipant를 만든다.
        var participants = new Dictionary<UnitId, BattleParticipant>();
        var firedActives = new Dictionary<UnitId, ActiveSkill>();
        foreach (var id in participating)
        {
            var u = state[id];
            // 행동불가(혼란)면 액티브도 못 쓴다(피격·방어는 정상).
            var (skill, newState) = dazedAtStart.Contains(id) || IsDazed(u)
                ? ((ActiveSkill?)null, u.State)
                : u.State.FiringActive();

            // 타격 액티브는 공격자만 쓴다(방어자만이면 보류·게이지 유지).
            if (skill?.Type == ActiveType.Strike && !attackers.Contains(id))
            {
                skill = null;
                newState = u.State;
            }

            if (skill is not null)
            {
                firedActives[id] = skill;
            }

            state[id] = u with { State = newState };
            var (effStats, outgoing) = ApplyDebuffs(u);
            participants[id] = new BattleParticipant(
                effStats with { Troops = u.Pool.Active },
                u.Field.Mode,
                u.Pool,
                u.Might,
                u.Intellect,
                u.MaxTroops,
                StrikeActive: skill?.Type == ActiveType.Strike ? skill : null,
                DefenseActive: skill?.Type == ActiveType.Defense ? skill : null,
                HealActive: skill?.Type == ActiveType.Heal ? skill : null,
                OutgoingDamagePercent: outgoing);
        }

        // 5) 동시 정산 → 병력 반영.
        var combat = _combat.Resolve(engagements, participants);
        foreach (var (id, pool) in combat.Pools)
        {
            state[id] = state[id] with { Pool = pool };
        }

        return new AdvanceTurn(Ordered(state), move, combat, firedActives, firedStratagems, statusDamage, stratagemDamage);
    }

    private static readonly IReadOnlyDictionary<UnitId, ActiveSkill> NoActives = new Dictionary<UnitId, ActiveSkill>();

    private static bool IsDazed(CombatUnit u) => u.State.Statuses.Any(s => s.IsDaze);

    // 이동 시뮬에 넣을 임시 FieldUnit. 혼란(행동불가)은 제자리에 묶고(속도 0·목표·모드 중립),
    // 수공(이동−1)은 속도를 깎는다(최소 1). 실제 Field는 위치만 되받아 보존한다.
    private static FieldUnit MovementField(CombatUnit u)
    {
        if (IsDazed(u))
        {
            return u.Field with { Mode = UnitMode.Advance, Target = null, Speed = 0 };
        }

        var moveDown = u.State.Statuses.Sum(s => s.MoveDownTiles);
        return moveDown > 0
            ? u.Field with { Speed = System.Math.Max(1, u.Field.Speed - moveDown) }
            : u.Field;
    }

    // 폭파: 대상 타일 반경 안의 다른 적 부대 전원에게 같은 즉발 피해(각 부대 지력이 저항). 대상 자신은
    // 이미 맞았으므로 제외. 서로 독립 피해라 순서가 결과를 바꾸지 않지만 결정론을 위해 id 순으로 돈다.
    private void ApplyAoe(Dictionary<UnitId, CombatUnit> state, Stratagem stratagem, CombatUnit caster, UnitId primaryTargetId,
        Dictionary<UnitId, int> stratagemDamage)
    {
        var center = state[primaryTargetId].Field.Position;
        foreach (var id in state.Keys.OrderBy(k => k.Value).ToList())
        {
            if (id == primaryTargetId)
            {
                continue;
            }

            var u = state[id];
            if (u.Field.Owner == caster.Field.Owner
                || u.Pool.Active <= 0
                || u.Field.Position.Distance(center) > stratagem.AoeRadius)
            {
                continue;
            }

            var dmg = stratagem.Damage(u.Pool.Active, caster.Intellect, u.Intellect);
            if (dmg > 0)
            {
                state[id] = u with { Pool = u.Pool.TakeDamage(dmg, _woundedPercent) };
                stratagemDamage[id] = stratagemDamage.GetValueOrDefault(id) + dmg;
            }
        }
    }

    // 교란: 대상을 시전자에게서 강도 배율만큼의 칸수만큼 밀어낸다. 매 스텝 시전자와의 거리를
    // 늘리는 이웃(고정 방향 순서 — 결정론) 중 통행 가능·비점유 칸으로 옮기고, 없으면 멈춘다(부분 후퇴).
    private void RepositionRetreat(Dictionary<UnitId, CombatUnit> state, UnitId targetId, CombatUnit caster, Stratagem stratagem)
    {
        var target = state[targetId];
        var strength = StratagemStrength.Percent(caster.Intellect, target.Intellect);
        var tiles = stratagem.RetreatTiles * strength / 100;
        if (tiles <= 0)
        {
            return;
        }

        var occupied = state.Values
            .Where(u => u.Id != targetId)
            .Select(u => u.Field.Position)
            .ToHashSet();

        var from = caster.Field.Position;
        var pos = target.Field.Position;
        for (var step = 0; step < tiles; step++)
        {
            var current = pos;
            var moved = false;
            foreach (var n in current.Neighbors())
            {
                if (n.Distance(from) <= current.Distance(from)
                    || occupied.Contains(n)
                    || !_movement.CanEnter(target.Field.Domain, n))
                {
                    continue;
                }

                pos = n;
                moved = true;
                break;
            }

            if (!moved)
            {
                break; // 막힘 — 밀려난 만큼만(부분 후퇴)
            }
        }

        if (pos != target.Field.Position)
        {
            state[targetId] = target with { Field = target.Field with { Position = pos } };
        }
    }

    // 지형 공방 보정(이동 후 위치·병종 분류)을 얹은 뒤, 걸린 능력치 디버프를 유효 능력치 + 준 피해
    // 배수로 접는다. 이간(무효)은 적성·가산 버킷을 100으로 되돌리고, 수공·연막은 준 피해를 곱으로 줄인다.
    private (CombatStats Stats, int OutgoingPercent) ApplyDebuffs(CombatUnit u)
    {
        // 전투는 이동 후 위치에서 벌어지므로 그 칸의 지형 보정을 여기서 반영한다(부대 스탯엔 지형 미포함).
        var (terrainAtk, terrainDf) = TerrainCombatBonus.For(u.Class, _terrainAt(u.Field.Position));
        var stats = u.Stats with
        {
            AtkStat = u.Stats.AtkStat + terrainAtk,
            DfStat = u.Stats.DfStat + terrainDf,
        };
        var outgoing = 100;
        foreach (var s in u.State.Statuses)
        {
            if (s.NullifyAptPassive)
            {
                stats = stats with { AptitudePercent = 100, AtkBonusPercent = 100, DfBonusPercent = 100 };
            }

            if (s.AtkDownPercent > 0 && (!s.RangedOnly || u.Field.AttackRange >= 2))
            {
                outgoing = outgoing * System.Math.Max(0, 100 - s.AtkDownPercent) / 100;
            }
        }

        return (stats, outgoing);
    }

    // 예약된 계략을 발동/캔슬한다. 발동한 시전 부대 → 그 계략의 사전을 돌려주고(그 교전 공격 불가),
    // 계략 즉발 피해를 <paramref name="stratagemDamage"/>에 대상별로 누적한다.
    private Dictionary<UnitId, Stratagem> FireStratagems(Dictionary<UnitId, CombatUnit> state, Dictionary<UnitId, int> stratagemDamage)
    {
        var casters = new Dictionary<UnitId, Stratagem>();
        foreach (var id in state.Keys.ToList())
        {
            var caster = state[id];
            var reservation = caster.State.Reservation;
            if (reservation is null)
            {
                continue;
            }

            var hasTarget = state.TryGetValue(reservation.TargetId, out var target);
            var valid = hasTarget && target!.Pool.Active > 0
                && caster.Field.Position.Distance(target.Field.Position) <= reservation.Stratagem.Range
                && reservation.Stratagem.CanCastOn(_terrainAt(target.Field.Position));

            switch (caster.State.StratagemDue(valid))
            {
                case StratagemFireOutcome.Fired:
                    var (stratagem, newState) = caster.State.FireStratagem();
                    state[id] = caster with { State = newState };
                    casters[id] = stratagem;

                    var t = state[reservation.TargetId];

                    // 지속 효과와 별개로 발동 시 터지는 추가 즉발 피해(수공 15%).
                    var burst = stratagem.InstantBurst(t.Pool.Active, caster.Intellect, t.Intellect);
                    if (burst > 0)
                    {
                        t = t with { Pool = t.Pool.TakeDamage(burst, _woundedPercent) };
                        state[reservation.TargetId] = t;
                        stratagemDamage[reservation.TargetId] = stratagemDamage.GetValueOrDefault(reservation.TargetId) + burst;
                    }

                    switch (stratagem.EffectKind)
                    {
                        // 즉발: 지금 피해. 지속(DoT): 상태를 걸고 다음 진행부터 tick. 정화: 걸린 상태 제거.
                        case StratagemEffectKind.InstantDamage:
                            var damage = stratagem.Damage(t.Pool.Active, caster.Intellect, t.Intellect);
                            if (damage > 0)
                            {
                                state[reservation.TargetId] = t with { Pool = t.Pool.TakeDamage(damage, _woundedPercent) };
                                stratagemDamage[reservation.TargetId] = stratagemDamage.GetValueOrDefault(reservation.TargetId) + damage;
                            }

                            // 폭파: 대상 반경 안의 다른 적 전원에게도 같은 즉발 피해(광역).
                            if (stratagem.AoeRadius > 0)
                            {
                                ApplyAoe(state, stratagem, caster, reservation.TargetId, stratagemDamage);
                            }

                            // 교란: 즉발 피해에 더해 강제 후퇴(부분).
                            if (stratagem.RetreatTiles > 0)
                            {
                                RepositionRetreat(state, reservation.TargetId, caster, stratagem);
                            }

                            break;

                        case StratagemEffectKind.DamageOverTime:
                        case StratagemEffectKind.Debuff:
                            var status = stratagem.MakeStatus(caster.Intellect, t.Intellect);
                            if (status is not null)
                            {
                                state[reservation.TargetId] = t with { State = t.State.AddStatus(status) };
                            }

                            break;

                        case StratagemEffectKind.Purge:
                            state[reservation.TargetId] = t with { State = t.State.Purge(stratagem.Purge) };
                            break;
                    }

                    break;

                case StratagemFireOutcome.Cancelled:
                    state[id] = caster with { State = caster.State.CancelStratagem() };
                    break;
            }
        }

        return casters;
    }

    // 이 진행에 전멸(병력 0)한 부대는 결과에서 뺀다 — 상위(Game)는 목록에서 사라진 부대를
    // 소멸(영혼 상승 연출·토큰 제거)로 처리한다.
    private static IReadOnlyList<CombatUnit> Ordered(Dictionary<UnitId, CombatUnit> state)
        => state.Values.Where(u => u.Pool.Active > 0).OrderBy(u => u.Id.Value).ToList();
}
