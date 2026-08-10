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

    /// <summary>
    /// 성·항구 공격 1회 교환(design-combat.md "성 전투"). 성벽이 서 있으면 건물dmg가 성벽에 흡수되고
    /// (초과분만 병력에 넘어감) 성은 정상 반격(유닛 공식, 전원 주100/부60)하며, 성벽이 0이면 유닛dmg가
    /// 수비 병력을 직접 치고 성 df가 <see cref="CastleState.CollapsedDf"/>로 격하되며 반격은 병력 비례
    /// 분할로 약해진다. <paramref name="attackers"/> index 0이 성 반격의 주대상(명령 순번).
    /// </summary>
    public SiegeOutcome ResolveSiege(IReadOnlyList<SiegeAttacker> attackers, CastleState castle)
    {
        var wallStanding = castle.WallCurrent > 0;
        var counter = new int[attackers.Count];

        if (wallStanding)
        {
            // 각 공격 부대가 성벽을 '전액' 친다(건물이라 다대일 60% 없음). 성 df는 성벽 방어값(12).
            var wallDamage = 0;
            for (var i = 0; i < attackers.Count; i++)
            {
                var a = attackers[i];
                wallDamage += DamageFormula.Resolve(
                    a.Troops, a.AtkBuilding, castle.WallDf,
                    new[] { a.AptitudePercent, a.AtkBonusPercent }, System.Array.Empty<int>());
            }

            var newWall = System.Math.Max(0, castle.WallCurrent - wallDamage);
            var overflow = System.Math.Max(0, wallDamage - castle.WallCurrent); // 성벽 넘는 초과분은 병력으로
            var absorbed = wallDamage - overflow;

            // 성 반격: 유닛 공식 그대로, 전원 타격(주100/부60). 사거리 밖(투석·공성탑 등)은 반격 안 받음.
            var castleStats = new CombatStats(castle.Troops, castle.UnitDmg, castle.WallDf, castle.AptitudePercent);
            for (var i = 0; i < attackers.Count; i++)
            {
                var a = attackers[i];
                counter[i] = a.InCounterRange
                    ? Damage(castleStats, new CombatStats(a.Troops, a.AtkUnit, a.Df, DfBonusPercent: a.DfBonusPercent), primaryTarget: i == 0)
                    : 0;
            }

            return new SiegeOutcome(true, absorbed, newWall, overflow, counter);
        }

        // 붕괴 단계: 유닛dmg가 수비 병력 직격, 성 df 6 격하.
        var troopDamage = 0;
        for (var i = 0; i < attackers.Count; i++)
        {
            var a = attackers[i];
            troopDamage += DamageFormula.Resolve(
                a.Troops, a.AtkUnit, castle.CollapsedDf,
                new[] { a.AptitudePercent, a.AtkBonusPercent }, System.Array.Empty<int>());
        }

        // 붕괴 반격 격하: 반격 총량을 공격 부대 병력 비율로 나눠 준다.
        var totalTroops = 0L;
        foreach (var a in attackers)
        {
            totalTroops += a.Troops;
        }

        var castleCollapsed = new CombatStats(castle.Troops, castle.UnitDmg, castle.CollapsedDf, castle.AptitudePercent);
        for (var i = 0; i < attackers.Count; i++)
        {
            var a = attackers[i];
            if (!a.InCounterRange || totalTroops == 0)
            {
                counter[i] = 0;
                continue;
            }

            var full = Damage(castleCollapsed, new CombatStats(a.Troops, a.AtkUnit, a.Df, DfBonusPercent: a.DfBonusPercent));
            counter[i] = (int)((long)full * a.Troops / totalTroops); // 병력 비율 분할
        }

        return new SiegeOutcome(false, 0, 0, troopDamage, counter);
    }
}
