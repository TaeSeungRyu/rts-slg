namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 한 "진행"을 이동 → 계략 발동 → 전투 페이즈 → 정산으로 묶는다(design-combat.md "전투 페이즈 발동"
/// 순환). 이동 시뮬을 돌려 정지시킨 뒤, 경과일만큼 발동 상태를 진행하고, 예약된 계략을 발동하며
/// (발동일엔 시전 부대 공격 불가), 사거리 전수검사로 교전을 만들어 액티브 발동(선봉 우선)을 얹어
/// 동시 정산한다. 결과로 위치·병력·발동 상태가 갱신된 부대를 돌려준다.
/// 지속 디버프/DoT 상태·정화·성 복귀 감지·Game 배선은 후속.
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

    public AdvanceTurn Run(IReadOnlyList<CombatUnit> units, int maxDays = 7)
    {
        // 1) 이동 — 진행 정지까지.
        var move = _movement.Advance(units.Select(u => u.Field).ToList(), maxDays);
        var moved = move.Units.ToDictionary(f => f.Id);

        // 2) 위치 갱신 + 경과일만큼 발동 상태 진행(야전 가정).
        var state = new Dictionary<UnitId, CombatUnit>();
        foreach (var u in units)
        {
            state[u.Id] = u with { Field = moved[u.Id], State = u.State.AdvanceField(move.Days) };
        }

        // 3) 전투 페이즈 발동 — 정지 시점 사거리 전수검사.
        var engagements = CombatPhase.DetectEngagements(state.Values.Select(u => u.Field).ToList());

        // 4) 계략 발동 — 예약이 발동일에 도달하면 대상 유효성으로 발동/캔슬. 발동 부대는 이번 교전
        //    공격을 하지 않고(발동일 공격 불가), 즉발·지속 피해 계략은 대상 병력을 즉시 깎는다.
        var firedStratagems = FireStratagems(state);
        engagements = engagements.Where(e => !firedStratagems.ContainsKey(e.Attacker)).ToList();

        if (engagements.Count == 0)
        {
            return new AdvanceTurn(Ordered(state), move, null, NoActives, firedStratagems);
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
            var (skill, newState) = u.State.FiringActive();

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
            participants[id] = new BattleParticipant(
                u.Stats with { Troops = u.Pool.Active },
                u.Field.Mode,
                u.Pool,
                u.Might,
                u.Intellect,
                u.MaxTroops,
                StrikeActive: skill?.Type == ActiveType.Strike ? skill : null,
                DefenseActive: skill?.Type == ActiveType.Defense ? skill : null,
                HealActive: skill?.Type == ActiveType.Heal ? skill : null);
        }

        // 5) 동시 정산 → 병력 반영.
        var combat = _combat.Resolve(engagements, participants);
        foreach (var (id, pool) in combat.Pools)
        {
            state[id] = state[id] with { Pool = pool };
        }

        return new AdvanceTurn(Ordered(state), move, combat, firedActives, firedStratagems);
    }

    private static readonly IReadOnlyDictionary<UnitId, ActiveSkill> NoActives = new Dictionary<UnitId, ActiveSkill>();

    // 예약된 계략을 발동/캔슬한다. 발동한 시전 부대 → 그 계략의 사전을 돌려준다(그 교전 공격 불가).
    private Dictionary<UnitId, Stratagem> FireStratagems(Dictionary<UnitId, CombatUnit> state)
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

                    // 즉발·지속 피해 계략만 즉시 피해(디버프/정화 상태 적용은 후속).
                    var t = state[reservation.TargetId];
                    var damage = stratagem.Damage(t.Pool.Active, caster.Intellect, t.Intellect);
                    if (damage > 0)
                    {
                        state[reservation.TargetId] = t with { Pool = t.Pool.TakeDamage(damage, _woundedPercent) };
                    }

                    break;

                case StratagemFireOutcome.Cancelled:
                    state[id] = caster with { State = caster.State.CancelStratagem() };
                    break;
            }
        }

        return casters;
    }

    private static IReadOnlyList<CombatUnit> Ordered(Dictionary<UnitId, CombatUnit> state)
        => state.Values.OrderBy(u => u.Id.Value).ToList();
}
