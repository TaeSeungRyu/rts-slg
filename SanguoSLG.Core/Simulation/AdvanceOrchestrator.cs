namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 한 "진행"을 이동 → 전투 페이즈 → 정산으로 묶는다(design-combat.md "전투 페이즈 발동" 순환).
/// 이동 시뮬을 돌려 정지시킨 뒤, 경과일만큼 발동 상태를 진행하고, 사거리 전수검사로 교전을 만들어
/// 액티브 발동(선봉 우선)을 얹어 동시 정산한다. 결과로 위치·병력·발동 상태가 갱신된 부대를 돌려준다.
/// 계략 발동/효과 적용과 성 복귀 감지는 후속(4c-4b)에서 얹는다.
/// </summary>
public sealed class AdvanceOrchestrator
{
    private readonly MovementSimulator _movement;
    private readonly CombatPhaseResolver _combat;

    public AdvanceOrchestrator(MovementSimulator movement, CombatPhaseResolver combat)
    {
        _movement = movement;
        _combat = combat;
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
        if (engagements.Count == 0)
        {
            return new AdvanceTurn(Ordered(state), move, null);
        }

        var attackers = engagements.Select(e => e.Attacker).ToHashSet();
        var participating = engagements
            .SelectMany(e => e.Targets.Append(e.Attacker))
            .ToHashSet();

        // 4) 교전 참가 부대마다 액티브 발동(선봉 우선)을 정하고 BattleParticipant를 만든다.
        var participants = new Dictionary<UnitId, BattleParticipant>();
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

        return new AdvanceTurn(Ordered(state), move, combat);
    }

    private static IReadOnlyList<CombatUnit> Ordered(Dictionary<UnitId, CombatUnit> state)
        => state.Values.OrderBy(u => u.Id.Value).ToList();
}
