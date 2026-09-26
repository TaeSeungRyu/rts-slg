namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed record RuinCombatExchange(string RuinId, UnitId Attacker, int DamageToRuin, int CounterDamage);
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
            if (!statuses.TryGetValue(ruin.Id, out var status) || status.Defenders <= 0 || status.IsProtected(state.Day)) continue;
            var defenders = status.Defenders;
            for (var i = 0; i < armies.Count && defenders > 0; i++)
            {
                var attacker = armies[i];
                if (!attacker.CanInitiateCombat || attacker.Pool.Active <= 0 || attacker.Field.Target != ruin.Position) continue;
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
                exchanges.Add(new(ruin.Id, attacker.Id, damage, counter));
                if (damage > 0) last[ruin.Id] = attacker.Field.Owner;
            }
            statuses[ruin.Id] = status with { Defenders = defenders };
        }

        var ordered = state.RuinStatus.Select(s => statuses.GetValueOrDefault(s.RuinId, s)).ToList();
        return new(state with { RuinStates = ordered }, armies, exchanges, last);
    }
}
