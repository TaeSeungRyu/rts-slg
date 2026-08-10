namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 야전 교전 정산(design-combat.md "전투 페이즈 발동" 5·"야전 다대일").
/// 성벽·스킬 발동은 상위 계층/후속 증분이 담당하고, 여기서는 이미 산출된
/// <see cref="CombatStats"/>를 받아 피해만 정산한다(엔진 비의존, 결정론).
/// </summary>
public sealed class BattleResolver
{
    private readonly int _secondaryPercent;

    /// <param name="multiTargetSecondaryPercent">
    /// 다대일에서 주대상 외 대상에 적용하는 배수(design-combat.md 60%). data/balance.json에서 온다.
    /// </param>
    public BattleResolver(int multiTargetSecondaryPercent)
    {
        _secondaryPercent = multiTargetSecondaryPercent;
    }

    /// <summary>공격자가 방어자에게 주는 피해(1:1).</summary>
    public int Damage(CombatStats attacker, CombatStats defender, bool primaryTarget = true)
    {
        var atkPercents = new List<int> { attacker.AptitudePercent, attacker.AtkBonusPercent };
        if (!primaryTarget)
        {
            atkPercents.Add(_secondaryPercent); // 다대일 부차 대상 60%
        }

        return DamageFormula.Resolve(
            attacker.Troops,
            attacker.AtkStat,
            defender.DfStat,
            atkPercents,
            new[] { defender.DfBonusPercent });
    }

    /// <summary>
    /// 1:1 동시 교환. 라운드 시작 병력 스냅샷 기준으로 양쪽 피해를 함께 산출한다(선공 이득 없음).
    /// </summary>
    public (int DamageToA, int DamageToB) Exchange(CombatStats a, CombatStats b)
        => (Damage(b, a), Damage(a, b));

    /// <summary>
    /// 야전 다대일: 한 부대(<paramref name="attacker"/>)가 여러 적과 교전한다.
    /// <paramref name="targets"/>는 명령 순번 순서 — index 0이 주대상(100%), 나머지는 60%.
    /// 반환은 각 대상이 받는 피해(입력 순서와 동일).
    /// </summary>
    public IReadOnlyList<int> DamageManyTargets(CombatStats attacker, IReadOnlyList<CombatStats> targets)
    {
        var result = new int[targets.Count];
        for (var i = 0; i < targets.Count; i++)
        {
            result[i] = Damage(attacker, targets[i], primaryTarget: i == 0);
        }

        return result;
    }
}
