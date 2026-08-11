namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 전투 페이즈 발동으로 만든 교전들을 <b>동시에</b> 정산한다(design-combat.md "정산 순서" 4~5).
/// 모든 피해를 라운드 시작 병력 스냅샷으로 계산한 뒤 일괄 적용해 선공 이득이 없다. 다대일은
/// 주대상 100%/나머지 60%(BattleResolver), 행군 방어자는 받는 피해가 준다(반격은 애초에 안 함).
/// 액티브·회복은 후속.
/// </summary>
public sealed class CombatPhaseResolver
{
    private readonly BattleResolver _resolver;
    private readonly int _woundedPercent;
    private readonly int _marchDamageTakenPercent;

    /// <param name="resolver">피해 공식 정산기.</param>
    /// <param name="woundedPercent">피해 중 부상 전환 비율(design-combat.md 70%).</param>
    /// <param name="marchDamageTakenPercent">행군 방어자가 받는 피해 비율(design-movement.md 70%).</param>
    public CombatPhaseResolver(BattleResolver resolver, int woundedPercent, int marchDamageTakenPercent = 70)
    {
        _resolver = resolver;
        _woundedPercent = woundedPercent;
        _marchDamageTakenPercent = marchDamageTakenPercent;
    }

    public CombatPhaseResult Resolve(
        IReadOnlyList<UnitEngagement> engagements,
        IReadOnlyDictionary<UnitId, BattleParticipant> participants)
    {
        // 1) 스냅샷 기준으로 부대별 받는 피해를 모두 누적한다(아직 적용 안 함 = 동시).
        var damage = new Dictionary<UnitId, int>();
        foreach (var engagement in engagements)
        {
            var attacker = participants[engagement.Attacker];
            for (var i = 0; i < engagement.Targets.Count; i++)
            {
                var targetId = engagement.Targets[i];
                var target = participants[targetId];

                var dealt = _resolver.Damage(attacker.Stats, target.Stats, primaryTarget: i == 0);
                if (target.Mode == UnitMode.March)
                {
                    dealt = dealt * _marchDamageTakenPercent / 100; // 행군 통과 피해
                }

                damage[targetId] = damage.GetValueOrDefault(targetId) + dealt;
            }
        }

        // 2) 누적 피해를 일괄 적용한다(소실/부상 전환).
        var pools = new Dictionary<UnitId, TroopPool>();
        foreach (var (id, participant) in participants)
        {
            var taken = damage.GetValueOrDefault(id);
            pools[id] = taken > 0 ? participant.Pool.TakeDamage(taken, _woundedPercent) : participant.Pool;
        }

        return new CombatPhaseResult(damage, pools);
    }
}
