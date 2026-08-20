namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 한 "진행"을 이동 → 계략 발동 → 전투 페이즈 → 정산으로 묶는다(design-combat.md "전투 페이즈 발동"
/// 순환). 이동 시뮬을 돌려 정지시킨 뒤, 경과일만큼 발동 상태를 진행하고, 예약된 계략을 발동하며
/// (발동일엔 시전 부대 공격 불가), 사거리 전수검사로 교전을 만들어 액티브 발동(선봉 우선)을 얹어
/// 동시 정산한다. 결과로 위치·병력·발동 상태가 갱신된 부대를 돌려준다. 지속 상태(DoT·능력치
/// 디버프·행동불가), 정화, 강제 후퇴(교란)까지 반영하고, 병력 0(전멸) 부대는 결과에서 뺀다(소멸 —
/// Game이 영혼 상승 연출로 처리). 아군 성 입성은 이동 단계에서 확정되어 성 복귀 초기화 후
/// EnteredCastle로 보고된다(수비 합류는 성 상태를 가진 상위 계층이 처리).
/// </summary>
public sealed class AdvanceOrchestrator
{
    private readonly MovementSimulator _movement;
    private readonly CombatPhaseResolver _combat;
    private readonly int _woundedPercent;
    private readonly Func<HexCoord, TerrainType> _terrainAt;
    private readonly int _provisionsPer10kPerDay;
    private readonly int _starvationLossPercentPerDay;
    private readonly int _resupplyRadius;
    private readonly MoraleConfig _morale;

    public AdvanceOrchestrator(
        MovementSimulator movement,
        CombatPhaseResolver combat,
        int woundedPercent = 70,
        Func<HexCoord, TerrainType>? terrainAt = null,
        int provisionsPer10kPerDay = 10,
        int starvationLossPercentPerDay = 5,
        int resupplyRadius = 6,
        MoraleConfig? morale = null,
        int reinforcePercent = 20)
    {
        _movement = movement;
        _combat = combat;
        _woundedPercent = woundedPercent;
        _terrainAt = terrainAt ?? (_ => TerrainType.Plains);
        _provisionsPer10kPerDay = provisionsPer10kPerDay;
        _starvationLossPercentPerDay = starvationLossPercentPerDay;
        _resupplyRadius = resupplyRadius;
        _morale = morale ?? new MoraleConfig();
        _reinforcePercent = reinforcePercent;
    }

    private readonly int _reinforcePercent;

    public AdvanceTurn Run(IReadOnlyList<CombatUnit> units, int maxDays = 7,
        IReadOnlyList<SiegeSite>? castles = null)
    {
        // 병력 0(전멸) 부대는 진행에서 빠진다 — 이동·전투·점유·표적에서 모두 제외한다.
        units = units.Where(u => u.Pool.Active > 0).ToList();

        // 0.9) 보급부대 자동 보충(이동 선행 — design-unit-state 1단계-보급): 보급부대가 반경 내 아군
        //      저(低)군량 부대에 하루치씩 채워준다(보급부대 재고 한도). 이동·소모보다 먼저 일어난다.
        units = Resupply(units);

        // 사기 증감 검산용: 이 진행 시작 병력(피해율 계산).
        var startTroops = units.ToDictionary(u => u.Id, u => u.Pool.Active);

        // 진행 시작 시점에 행동불가(혼란)인 부대 — 이동·전투 모두 이 스냅샷으로 판정한다
        // (상태 tick이 진행 중간에 남은 진행을 줄여도, 그 진행의 효과는 온전히 적용).
        var dazedAtStart = units.Where(IsDazed).Select(u => u.Id).ToHashSet();

        // 패주(사기<임계) 부대 — 이번 진행 공격 불가·강제 후퇴(적 반대). 진행 시작 스냅샷으로 판정.
        var routedAtStart = units.Where(u => u.Routed).Select(u => u.Id).ToHashSet();

        // 1) 이동 — 진행 정지까지. 걸린 상태(혼란=행동불가, 수공=이동−1)를 이동 입력에 반영한다.
        var move = _movement.Advance(units.Select(MovementField).ToList(), maxDays, castles);
        var moved = move.Units.ToDictionary(f => f.Id);

        // 1.5) 아군 성 입성(이동 단계에서 확정) — 야전에서 빠지고 성 복귀 초기화(게이지 0·모략력
        //      충전·예약 취소·지속 상태 해제)를 적용한다. 이후 상태 틱·계략·전투에 끼지 않는다.
        var enteredIds = move.EnteredCastle.ToHashSet();
        var enteredCastle = units
            .Where(u => enteredIds.Contains(u.Id))
            .Select(u => u with { State = u.State.ReturnToCastle(), Morale = 100, Routed = false })
            .ToList();

        // 2) 위치만 갱신(임시 이동 스탯은 버림) + 경과일만큼 발동 상태 진행(야전 가정).
        var state = new Dictionary<UnitId, CombatUnit>();
        foreach (var u in units.Where(u => !enteredIds.Contains(u.Id)))
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

        // 2.6) 군량 소모(design-unit-state 1단계) — 추적 부대만. 경과일×병력 비례로 휴대 군량을 깎고,
        //      바닥나면 그 진행 동안 굶주려 이탈(병력 손실, 부상 없이 소실). 미추적(−1)은 무한 보급 가정.
        var starvation = new Dictionary<UnitId, int>();
        foreach (var id in state.Keys.ToList())
        {
            var u = state[id];
            if (!u.TracksProvisions || move.Days <= 0)
            {
                continue;
            }

            var eaten = u.Pool.Active * _provisionsPer10kPerDay * move.Days / 10000;
            var remaining = u.Provisions - eaten;
            if (remaining >= 0)
            {
                state[id] = u with { Provisions = remaining };
                continue;
            }

            // 고갈 — 그 진행 굶주림: 병력의 (이탈률 × 경과일)%를 잃는다(이탈은 부상 없이 소실).
            var lossPercent = System.Math.Min(100, _starvationLossPercentPerDay * move.Days);
            var lost = u.Pool.Active * lossPercent / 100;
            state[id] = u with { Provisions = 0, Pool = u.Pool.TakeDamage(lost, woundedPercent: 0) };
            if (lost > 0)
            {
                starvation[id] = lost;
            }
        }

        // 2.7) 패주 강제 후퇴(design-unit-state 2단계): 패주 부대를 가장 가까운 적 반대로 밀어낸다.
        foreach (var id in routedAtStart.OrderBy(x => x.Value))
        {
            if (!state.TryGetValue(id, out var u) || u.Pool.Active <= 0)
            {
                continue;
            }

            var enemy = state.Values
                .Where(o => o.Field.Owner != u.Field.Owner && o.Pool.Active > 0)
                .OrderBy(o => o.Field.Position.Distance(u.Field.Position)).ThenBy(o => o.Id.Value)
                .FirstOrDefault();
            if (enemy is not null)
            {
                PushAway(state, id, enemy.Field.Position, 2);
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
                && !(dazedAtStart.Contains(e.Attacker) || IsDazed(state[e.Attacker]))
                && !routedAtStart.Contains(e.Attacker)) // 패주 부대는 공격 못 한다(피격·방어는 정상)
            .ToList();

        if (engagements.Count == 0)
        {
            SyncCargo(state);
            var moraleOnly = ApplyMoraleAndRout(state, startTroops, NoEngagements, combat: null, starvation);
            var reinforcedOnly = Reinforce(state);
            return new AdvanceTurn(Ordered(state), move, null, NoActives, firedStratagems, statusDamage, stratagemDamage, enteredCastle, starvation, moraleOnly, reinforcedOnly);
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

        // 5.5) 보급부대 균일 피해 분배 — 이 진행의 손실(전투·DoT·굶주림)을 병종 구성에 반영.
        SyncCargo(state);

        // 6) 사기 증감·패주 전이(전투 이후) — design-unit-state 2단계.
        var moraleChange = ApplyMoraleAndRout(state, startTroops, engagements, combat, starvation);

        // 7) 병력보충(교전 정산이 끝난 뒤) — design-unit-state "병력보충 명령".
        var reinforced = Reinforce(state);

        return new AdvanceTurn(Ordered(state), move, combat, firedActives, firedStratagems, statusDamage, stratagemDamage, enteredCastle, starvation, moraleChange, reinforced);
    }

    // 보급부대 손실을 병종 구성에 균일(병력 비례)하게 분배한다 — 한 병종만 갈려나가지 않는다
    // (design-unit-state 1단계-보급). 몫의 잔여는 구성 순서(병종 코드 정렬)대로 1씩 — 결정론.
    private static void SyncCargo(Dictionary<UnitId, CombatUnit> state)
    {
        foreach (var id in state.Keys.OrderBy(k => k.Value).ToList())
        {
            var u = state[id];
            if (!u.IsSupply || u.Cargo.Count == 0)
            {
                continue;
            }

            var total = u.Cargo.Sum(c => c.Troops);
            var loss = total - u.Pool.Active;
            if (loss <= 0)
            {
                continue;
            }

            var cargo = u.Cargo.Select(c => c with { Troops = c.Troops - (int)((long)loss * c.Troops / total) }).ToList();
            var remainder = cargo.Sum(c => c.Troops) - u.Pool.Active;
            for (var i = 0; remainder > 0 && i < cargo.Count; i++)
            {
                if (cargo[i].Troops > 0)
                {
                    cargo[i] = cargo[i] with { Troops = cargo[i].Troops - 1 };
                    remainder--;
                }
            }

            state[id] = u with { SupplyCargo = cargo.Where(c => c.Troops > 0).ToList() };
        }
    }

    // 병력보충: 대상이 1칸 이내 아군이고 보급부대가 같은 병종을 보유하면, 그 병종의 일정 %를
    // 대상에 충원한다(대상 총원 상한). 대상 훈련도는 가중 평균. 결정론: 보급부대 id 오름차순.
    private Dictionary<UnitId, int> Reinforce(Dictionary<UnitId, CombatUnit> state)
    {
        var transferred = new Dictionary<UnitId, int>();
        foreach (var id in state.Keys.OrderBy(k => k.Value).ToList())
        {
            var supply = state[id];
            if (!supply.IsSupply || supply.ReinforceTarget is not { } targetId || supply.Pool.Active <= 0
                || !state.TryGetValue(targetId, out var target) || target.Pool.Active <= 0
                || target.Field.Owner != supply.Field.Owner
                || target.Field.Position.Distance(supply.Field.Position) > 1)
            {
                continue;
            }

            var idx = supply.Cargo.ToList().FindIndex(c => c.TroopCode == target.TroopCode && c.Troops > 0);
            if (idx < 0)
            {
                continue;
            }

            var comp = supply.Cargo[idx];
            var room = target.MaxTroops - target.Pool.Active;
            var give = System.Math.Min(comp.Troops * _reinforcePercent / 100, room);
            if (give <= 0)
            {
                continue;
            }

            var newActive = target.Pool.Active + give;
            var training = (int)(((long)target.Training * target.Pool.Active + (long)comp.TrainingLevel * give + newActive / 2) / newActive);
            state[targetId] = target with
            {
                Pool = target.Pool with { Active = newActive },
                Training = training,
            };

            var cargo = supply.Cargo.ToList();
            cargo[idx] = comp with { Troops = comp.Troops - give };
            state[id] = supply with
            {
                Pool = supply.Pool with { Active = supply.Pool.Active - give },
                SupplyCargo = cargo.Where(c => c.Troops > 0).ToList(),
            };
            transferred[targetId] = transferred.GetValueOrDefault(targetId) + give;
        }

        return transferred;
    }

    private static readonly IReadOnlyList<UnitEngagement> NoEngagements = new List<UnitEngagement>();

    // 사기 증감·패주(design-unit-state 2단계): 피해율↓ / 교전 우세·격파↑ / 굶주림↓ / 무전투 휴식↑.
    // 그 뒤 사기<임계면 패주 진입, ≥회복 임계면 해제(히스테리시스). 생존 부대만. 결정론: id 순.
    private Dictionary<UnitId, int> ApplyMoraleAndRout(Dictionary<UnitId, CombatUnit> state,
        Dictionary<UnitId, int> startTroops, IReadOnlyList<UnitEngagement> engagements,
        CombatPhaseResult? combat, Dictionary<UnitId, int> starvation)
    {
        var attackerTargets = engagements.ToDictionary(e => e.Attacker, e => e.Targets);
        var engaged = engagements.SelectMany(e => e.Targets.Append(e.Attacker)).ToHashSet();
        var changes = new Dictionary<UnitId, int>();

        foreach (var id in state.Keys.OrderBy(k => k.Value).ToList())
        {
            var u = state[id];
            if (u.Pool.Active <= 0)
            {
                continue; // 전멸 부대는 소멸(사기 무의미)
            }

            var start = startTroops.GetValueOrDefault(id, u.Pool.Active);
            var lostPct = start > 0 ? (start - u.Pool.Active) * 100 / start : 0;

            var delta = 0;
            if (lostPct > 0)
            {
                delta -= lostPct * _morale.DamageLossNum / _morale.DamageLossDen;
            }

            if (starvation.ContainsKey(id))
            {
                delta -= _morale.StarveLoss;
            }

            if (attackerTargets.TryGetValue(id, out var targets)
                && targets.Any(t => !state.TryGetValue(t, out var tv) || tv.Pool.Active <= 0))
            {
                delta += _morale.KillGain; // 격파
            }

            if (engaged.Contains(id) && lostPct < 10)
            {
                delta += _morale.WinGain; // 우세(적게 잃고 교전)
            }
            else if (!engaged.Contains(id) && lostPct == 0 && !starvation.ContainsKey(id))
            {
                delta += _morale.RestGain; // 무전투 휴식
            }

            var morale = System.Math.Clamp(u.Morale + delta, 0, 100);
            var routed = morale < _morale.RoutThreshold || (u.Routed && morale < _morale.RoutRecover);
            // 패주하면 명령(목표)을 취소한다 — 도망친 부대가 사기를 회복해도 스스로 다시
            // 진군하지 않는다(2026-08-20 사용자 결정). 재출동은 플레이어가 다시 명령한다.
            var field = routed ? u.Field with { Target = null } : u.Field;
            state[id] = u with { Morale = morale, Routed = routed, Field = field };
            if (delta != 0)
            {
                changes[id] = delta;
            }
        }

        return changes;
    }

    private static readonly IReadOnlyDictionary<UnitId, ActiveSkill> NoActives = new Dictionary<UnitId, ActiveSkill>();

    private static bool IsDazed(CombatUnit u) => u.State.Statuses.Any(s => s.IsDaze);

    // 이동 시뮬에 넣을 임시 FieldUnit. 혼란(행동불가)은 제자리에 묶고(속도 0·목표·모드 중립),
    // 수공(이동−1)은 속도를 깎는다(최소 1). 실제 Field는 위치만 되받아 보존한다.
    private static FieldUnit MovementField(CombatUnit u)
    {
        // 패주·행동불가(혼란)는 목표를 향한 전진을 멈춘다(패주는 이후 적 반대로 강제 후퇴).
        if (IsDazed(u) || u.Routed)
        {
            return u.Field with { Mode = UnitMode.Advance, Target = null, Speed = 0 };
        }

        var moveDown = u.State.Statuses.Sum(s => s.MoveDownTiles);
        return moveDown > 0
            ? u.Field with { Speed = System.Math.Max(1, u.Field.Speed - moveDown) }
            : u.Field;
    }

    // 보급부대 자동 보충: 각 보급부대가 반경 내 아군(자신 제외·군량 추적) 중 최대치 미만인 부대에
    // 하루치(병력 비례)씩 재고 한도 안에서 채워준다. 결정론: 보급부대·수혜 부대 모두 id 오름차순.
    private IReadOnlyList<CombatUnit> Resupply(IReadOnlyList<CombatUnit> units)
    {
        if (!units.Any(u => u.IsSupply && u.Provisions > 0))
        {
            return units;
        }

        var byId = units.ToDictionary(u => u.Id);
        foreach (var supply in units.Where(u => u.IsSupply && u.Provisions > 0).OrderBy(u => u.Id.Value))
        {
            var stock = byId[supply.Id].Provisions;
            foreach (var ally in units
                .Where(a => a.Field.Owner == supply.Field.Owner && a.Id != supply.Id && a.TracksProvisions
                    && a.Field.Position.Distance(supply.Field.Position) <= _resupplyRadius)
                .OrderBy(a => a.Id.Value))
            {
                if (stock <= 0)
                {
                    break;
                }

                var recipient = byId[ally.Id];
                var deficit = recipient.MaxProvisions() - recipient.Provisions;
                if (deficit <= 0)
                {
                    continue;
                }

                var oneDay = recipient.Pool.Active * _provisionsPer10kPerDay / 10000;
                var give = System.Math.Min(System.Math.Min(oneDay, deficit), stock);
                if (give <= 0)
                {
                    continue;
                }

                byId[ally.Id] = recipient with { Provisions = recipient.Provisions + give };
                stock -= give;
            }

            byId[supply.Id] = byId[supply.Id] with { Provisions = stock };
        }

        return units.Select(u => byId[u.Id]).ToList();
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
        var strength = StratagemStrength.Percent(caster.Intellect, state[targetId].Intellect);
        var tiles = stratagem.RetreatTiles * strength / 100;
        PushAway(state, targetId, caster.Field.Position, tiles);
    }

    // 대상을 <paramref name="fromPos"/>에서 멀어지는 방향으로 <paramref name="tiles"/>칸 밀어낸다. 매 스텝
    // 거리를 늘리는 이웃(고정 방향 순서 — 결정론) 중 통행 가능·비점유 칸으로. 막히면 그만큼만(부분 후퇴).
    private void PushAway(Dictionary<UnitId, CombatUnit> state, UnitId targetId, HexCoord fromPos, int tiles)
    {
        if (tiles <= 0)
        {
            return;
        }

        var target = state[targetId];
        var occupied = state.Values.Where(u => u.Id != targetId).Select(u => u.Field.Position).ToHashSet();
        var pos = target.Field.Position;
        for (var step = 0; step < tiles; step++)
        {
            var current = pos;
            var moved = false;
            foreach (var n in current.Neighbors())
            {
                if (n.Distance(fromPos) <= current.Distance(fromPos)
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

        // 사기·훈련 공/방 배수(design-unit-state 2·3단계): 공격은 준 피해에, 방어는 df에 +보너스%.
        var quality = MoraleBonusPercent(u.Morale) + TrainingBonusPercent(u.Training);
        if (quality != 0)
        {
            outgoing = System.Math.Max(0, outgoing * (100 + quality) / 100);
            stats = stats with { DfStat = System.Math.Max(1, stats.DfStat * (100 + quality) / 100) };
        }

        return (stats, outgoing);
    }

    private int MoraleBonusPercent(int morale) => (morale - 50) * _morale.MoraleBonusNum / _morale.MoraleBonusDen;

    private int TrainingBonusPercent(int training) => (training - 50) * _morale.TrainingBonusNum / _morale.TrainingBonusDen;

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
