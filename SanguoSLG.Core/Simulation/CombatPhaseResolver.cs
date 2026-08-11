namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 전투 페이즈 발동으로 만든 교전들을 <b>동시에</b> 정산한다(design-combat.md "정산 순서" 4~5).
/// 모든 피해를 라운드 시작 병력 스냅샷으로 계산한 뒤 일괄 적용해 선공 이득이 없다. 다대일은
/// 주대상 100%/나머지 60%(BattleResolver), 행군 방어자는 받는 피해가 준다(반격은 애초에 안 함).
/// 정산 순서(방어>회복>계략>공격)를 따른다: 방어 액티브로 받는 피해를 줄이고, 회복 액티브로 부상
/// 풀에서 병력을 늘려 공격 스냅샷에 반영하며, 타격 액티브는 주대상 공격을 대체한다. 계략 디버프는
/// 이미 Stats에 반영된 것으로 본다. 어떤 액티브가 발동하는지(게이지·예약)는 상위가 정해 넘긴다(4c-3b).
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
        // 1) 방어 — 부대별 받는 피해 배수. 2) 회복 — 부상 풀에서 늘린 병력으로 공격 스냅샷을 만든다.
        var takenPercent = new Dictionary<UnitId, int>();
        var healMoved = new Dictionary<UnitId, int>();
        var attackStats = new Dictionary<UnitId, CombatStats>();
        foreach (var (id, p) in participants)
        {
            takenPercent[id] = p.DefenseActive is null ? 100 : BattleResolver.DamageTakenPercent(p.DefenseActive, p.Might);

            var desiredHeal = p.HealActive is null ? 0 : BattleResolver.HealAmount(p.HealActive, p.Intellect, p.MaxTroops);
            healMoved[id] = System.Math.Min(desiredHeal, p.Pool.Wounded);
            attackStats[id] = p.Stats with { Troops = p.Pool.Active + healMoved[id] };
        }

        // 3) 계략 디버프는 이미 Stats에 반영. 4) 공격 — 스냅샷 기준 동시 누적.
        var damage = new Dictionary<UnitId, int>();
        var dealt = new Dictionary<UnitId, int>();
        foreach (var engagement in engagements)
        {
            var attacker = participants[engagement.Attacker];
            for (var i = 0; i < engagement.Targets.Count; i++)
            {
                var targetId = engagement.Targets[i];
                var target = participants[targetId];

                // 타격 액티브는 주대상 공격을 대체한다(부차 대상은 일반 60%).
                var raw = i == 0 && attacker.StrikeActive is not null
                    ? _resolver.StrikeDamage(attackStats[engagement.Attacker], attackStats[targetId], attacker.StrikeActive, attacker.Might, attacker.TargetIsBuilding)
                    : _resolver.Damage(attackStats[engagement.Attacker], attackStats[targetId], primaryTarget: i == 0);

                if (target.Mode == UnitMode.March)
                {
                    raw = raw * _marchDamageTakenPercent / 100; // 행군 통과 피해
                }

                raw = raw * takenPercent[targetId] / 100; // 방어 액티브 감소
                damage[targetId] = damage.GetValueOrDefault(targetId) + raw;
                dealt[engagement.Attacker] = dealt.GetValueOrDefault(engagement.Attacker) + raw;
            }
        }

        // 5) 회복(부상→활성) 후 누적 피해를 일괄 적용한다(소실/부상 전환).
        var pools = new Dictionary<UnitId, TroopPool>();
        foreach (var (id, p) in participants)
        {
            var pool = healMoved[id] > 0 ? p.Pool.Heal(healMoved[id]) : p.Pool;
            var taken = damage.GetValueOrDefault(id);
            pools[id] = taken > 0 ? pool.TakeDamage(taken, _woundedPercent) : pool;
        }

        return new CombatPhaseResult(damage, dealt, pools);
    }
}
