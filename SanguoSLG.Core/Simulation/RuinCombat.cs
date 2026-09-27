namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed record RuinCombatExchange(string RuinId, UnitId Attacker, int DamageToRuin, int CounterDamage,
    bool Captured = false);
public sealed record RuinCombatResult(GameState State, IReadOnlyList<CombatUnit> Armies,
    IReadOnlyList<RuinCombatExchange> Exchanges, IReadOnlyDictionary<string, FactionId> LastAttackers);

/// <summary>유적은 움직이거나 선공하지 않고, 자신을 공격한 부대에 사거리와 무관하게 반격한다.</summary>
public sealed class RuinCombat
{
    private readonly BattleResolver _battle;
    private readonly int _woundedPercent;

    public RuinCombat(BattleResolver battle, int woundedPercent = 70)
    {
        _battle = battle;
        _woundedPercent = woundedPercent;
    }

    public RuinCombatResult Resolve(GameState state, IReadOnlyList<CombatUnit> input)
    {
        var armies = input.ToList();
        var statuses = state.RuinStatus.ToDictionary(r => r.RuinId, StringComparer.Ordinal);
        var exchanges = new List<RuinCombatExchange>();
        var last = new Dictionary<string, FactionId>(StringComparer.Ordinal);

        foreach (var ruin in state.Ruins.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            if (!statuses.TryGetValue(ruin.Id, out var status)) continue;
            if (status.Defenders <= 0 && status.Owner is not null
                && status.ProtectedUntilDay is { } until && state.Day >= until)
            {
                status = status with { Defenders = ruin.MaxDefenders, ProtectedUntilDay = null };
                statuses[ruin.Id] = status;
            }
            if (status.Defenders <= 0 || status.IsProtected(state.Day)) continue;
            var defenders = status.Defenders;
            for (var i = 0; i < armies.Count && defenders > 0; i++)
            {
                var attacker = armies[i];
                // 현재 규칙에서는 유적 타일에 진입할 수 없고 Target을 유지한 채 사거리에서 공격한다.
                // Position 일치는 이전 저장 데이터에 이미 유적 위에 선 부대가 있을 때의 호환 처리다.
                var targetsRuin = attacker.Field.Target == ruin.Position || attacker.Field.Position == ruin.Position;
                if (!attacker.CanInitiateCombat || attacker.Pool.Active <= 0 || !targetsRuin) continue;
                if (status.Owner == attacker.Field.Owner) continue;
                if (attacker.Field.Position.Distance(ruin.Position) > attacker.Field.AttackRange) continue;

                var ruinStats = new CombatStats(defenders, AtkStat: 8, DfStat: 10,
                    AptitudePercent: AptitudeGrade.A.Percent());
                var attackerStats = attacker.Stats with { Troops = attacker.Pool.Active };
                var damage = _battle.Damage(attackerStats, ruinStats);
                var counter = _battle.Damage(ruinStats, attackerStats); // 거리와 무관한 필수 반격
                defenders = Math.Max(0, defenders - damage);
                var pool = attacker.Pool.TakeDamage(counter, _woundedPercent);
                armies[i] = attacker with { Pool = pool, Stats = attacker.Stats with { Troops = pool.Active } };
                exchanges.Add(new(ruin.Id, attacker.Id, damage, counter, Captured: defenders <= 0 && damage > 0));
                if (damage > 0) last[ruin.Id] = attacker.Field.Owner;
            }
            if (defenders <= 0 && last.TryGetValue(ruin.Id, out var captor))
            {
                var registrations = status.Registrations.Append(captor).Distinct().ToList();
                statuses[ruin.Id] = status with
                {
                    Defenders = 0,
                    Owner = captor,
                    CapturedDay = state.Day,
                    ProtectedUntilDay = state.Day + 30,
                    RegisteredFactions = registrations,
                };
            }
            else statuses[ruin.Id] = status with { Defenders = defenders };
        }

        var ordered = state.RuinStatus.Select(s => statuses.GetValueOrDefault(s.RuinId, s)).ToList();
        // 유적 반격으로 전멸한 부대는 같은 공격 처리에서 즉시 야전 목록에서 제거한다.
        // 0명 토큰이 다음 진행까지 남지 않으며 표현 계층도 이 턴의 전멸/해골 연출을 예약한다.
        var survivors = armies.Where(a => a.Pool.Active > 0).ToList();
        return new(state with { RuinStates = ordered }, survivors, exchanges, last);
    }
}
