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
    /// 타격형 액티브가 대체 공격으로 주는 피해(design-skill-actives.md). 정상 공격 피해에 배수와
    /// 무력 스케일을 곱한다. 대상 df 감소(일섬·파갑)와 병력 비례 처형(참)을 반영한다.
    /// </summary>
    public int StrikeDamage(CombatStats attacker, CombatStats defender, ActiveSkill skill, int might, bool targetIsBuilding = false)
    {
        var m = StatScale.Percent(might);

        if (skill.ExecutePercent > 0)
        {
            // 병력 비례 처형(atk 무관): min(처형% × M, 상한%)만큼 대상 병력을 깎는다.
            var pct = System.Math.Min(skill.ExecutePercent * m / 100, skill.ExecuteCapPercent);
            return (int)((long)defender.Troops * pct / 100);
        }

        var mult = skill.BuildingOnly && !targetIsBuilding ? 100 : skill.DamageMultPercent;
        var effectiveDf = System.Math.Max(1, defender.DfStat * (100 - skill.DefenderDfReductionPercent) / 100);
        var normal = Damage(attacker, defender with { DfStat = effectiveDf });
        return (int)((long)normal * mult * m / 10000); // ÷100(배수) ÷100(M)
    }

    /// <summary>
    /// 방어형 액티브가 받는 피해에 곱하는 배수 퍼센트(design-skill-actives.md). 철벽 -30%(무력 60) → 70.
    /// 감소율에 무력 스케일을 곱하되 최대 -75%(하한 25)까지.
    /// </summary>
    public static int DamageTakenPercent(ActiveSkill skill, int might)
    {
        var reduction = System.Math.Min(skill.DamageReductionPercent * StatScale.Percent(might) / 100, 75);
        return 100 - reduction;
    }

    /// <summary>회복형 액티브의 병력 회복량(design-skill-actives.md). 최대 병력 대비 회복% × 지력 스케일, 상한 적용.</summary>
    public static int HealAmount(ActiveSkill skill, int intellect, int maxTroops)
    {
        var pct = System.Math.Min(skill.HealPercent * StatScale.Percent(intellect) / 100, skill.HealCapPercent);
        return (int)((long)maxTroops * pct / 100);
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
