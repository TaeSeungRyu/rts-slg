namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 1:1 교전 1회를 정산 순서대로 계산한다(design-combat.md "정산 순서": 방어 → 회복 → 계략 → 공격).
/// 방어·회복으로 상태를 만든 뒤, 모든 공격을 <b>라운드 시작 병력 스냅샷</b> 기준으로 동시 산출한다
/// (선공 이득 없음, 처리 순서가 결과를 바꾸지 않음). 계략 디버프는 이미 <see cref="Combatant.Stats"/>에
/// 반영된 것으로 본다.
/// </summary>
public sealed class EngagementResolver
{
    private readonly BattleResolver _resolver;

    public EngagementResolver(BattleResolver resolver) => _resolver = resolver;

    public ExchangeOutcome Resolve(Combatant a, Combatant b)
    {
        // 1. 방어 — 받는 피해 배수(액티브 없으면 100).
        var takenA = a.DefenseActive is null ? 100 : BattleResolver.DamageTakenPercent(a.DefenseActive, a.Might);
        var takenB = b.DefenseActive is null ? 100 : BattleResolver.DamageTakenPercent(b.DefenseActive, b.Might);

        // 2. 회복 — 병력을 먼저 늘려 이후 공격·피격 기준 병력에 반영한다.
        var healA = a.HealActive is null ? 0 : BattleResolver.HealAmount(a.HealActive, a.Intellect, a.MaxTroops);
        var healB = b.HealActive is null ? 0 : BattleResolver.HealAmount(b.HealActive, b.Intellect, b.MaxTroops);
        healA = System.Math.Min(healA, System.Math.Max(0, a.MaxTroops - a.Stats.Troops));
        healB = System.Math.Min(healB, System.Math.Max(0, b.MaxTroops - b.Stats.Troops));
        var statsA = a.Stats with { Troops = a.Stats.Troops + healA };
        var statsB = b.Stats with { Troops = b.Stats.Troops + healB };

        // 3. 계략 — 디버프는 이미 stats에 반영(2일 전 시전분).

        // 4. 공격 — 스냅샷(statsA/statsB) 기준 동시 산출. 타격 액티브면 대체, 아니면 일반.
        var rawAtoB = a.StrikeActive is null
            ? _resolver.Damage(statsA, statsB)
            : _resolver.StrikeDamage(statsA, statsB, a.StrikeActive, a.Might, a.TargetIsBuilding);
        var rawBtoA = b.StrikeActive is null
            ? _resolver.Damage(statsB, statsA)
            : _resolver.StrikeDamage(statsB, statsA, b.StrikeActive, b.Might, b.TargetIsBuilding);

        // 5. 방어 감소를 받는 쪽에 적용.
        var dmgToB = (int)((long)rawAtoB * takenB / 100);
        var dmgToA = (int)((long)rawBtoA * takenA / 100);

        return new ExchangeOutcome(dmgToA, dmgToB, healA, healB);
    }
}
