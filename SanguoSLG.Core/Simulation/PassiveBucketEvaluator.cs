namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 부대가 보유한 패시브 스킬들을 상황(<see cref="CombatContext"/>)에 맞춰 산출 ③ 가산 버킷으로 합산한다.
/// 선봉·부관 두 장수의 패시브가 모두 들어간다(호출자가 합쳐서 넘긴다). 조건 불충족 효과는 무시한다.
/// 반환은 <see cref="CombatStats.AtkBonusPercent"/>·<see cref="CombatStats.DfBonusPercent"/>에 바로 쓸 값.
/// </summary>
public static class PassiveBucketEvaluator
{
    public static (int AtkPercent, int DfPercent) Evaluate(
        IEnumerable<(PassiveSkill Skill, int Tier)> held,
        CombatContext context)
    {
        var atk = 0;
        var def = 0;

        foreach (var (skill, tier) in held)
        {
            foreach (var effect in skill.Effects)
            {
                if (!Applies(effect.Condition, context))
                {
                    continue;
                }

                if (effect.Bucket == SkillBucket.Attack)
                {
                    atk += effect.AmountAtTier(tier);
                }
                else
                {
                    def += effect.AmountAtTier(tier);
                }
            }
        }

        return (100 + atk, 100 + def);
    }

    private static bool Applies(PassiveCondition condition, CombatContext c) => condition switch
    {
        PassiveCondition.Always => true,
        PassiveCondition.TargetBuilding => c.TargetIsBuilding,
        PassiveCondition.TargetUnit => !c.TargetIsBuilding,
        PassiveCondition.Rough => c.OwnTerrainRough,
        PassiveCondition.PlainsDesert => c.OwnTerrainPlainsDesert,
        PassiveCondition.Momentum => c.IsMajoritySide,
        PassiveCondition.Pursuit => c.Pursuing,
        PassiveCondition.EnemyMarching => c.EnemyMarching,
        PassiveCondition.Melee => c.MeleeEngagement,
        PassiveCondition.MeleeIncoming => c.IncomingMelee,
        PassiveCondition.RangedIncoming => c.IncomingRanged,
        PassiveCondition.HpBelowHalf => c.HpRatioPercent <= 50,
        PassiveCondition.HpAboveHalf => c.HpRatioPercent > 50,
        PassiveCondition.CastleGarrison => c.InCastle,
        PassiveCondition.Surrounded => c.IsSurrounded,
        PassiveCondition.Field => c.InField,
        _ => throw new System.ArgumentOutOfRangeException(nameof(condition)),
    };
}
