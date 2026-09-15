namespace SanguoSLG.Core.Domain;

/// <summary>Phase 12 장수 성장 규칙. 레벨은 1~50, 패시브 스킬은 티어 1~3으로 성장한다.</summary>
public static class GeneralGrowth
{
    public const int MinLevel = 1;
    public const int MaxLevel = 50;
    public const int MaxPassiveTier = 3;
    public const int TrainGeneralExperience = 60;
    public const int TrainPassiveExperience = 30;

    /// <summary>다음 레벨 필요 경험치 = 100 + 현재 레벨 × 25.</summary>
    public static int RequiredExperienceForNextLevel(int currentLevel)
        => currentLevel >= MaxLevel ? int.MaxValue : 100 + System.Math.Clamp(currentLevel, MinLevel, MaxLevel) * 25;

    /// <summary>레벨 공격/방어 보정. Lv1=0, Lv50=4.9.</summary>
    public static double LevelCombatBonus(int level)
        => (System.Math.Clamp(level, MinLevel, MaxLevel) - 1) * 0.1d;

    /// <summary>소수 보정을 정수 전투값에 더할 때 사용하는 반올림 누적값.</summary>
    public static int LevelCombatBonusRounded(int level)
        => (int)System.Math.Round(LevelCombatBonus(level), MidpointRounding.AwayFromZero);

    /// <summary>패시브 다음 티어 필요 경험치. 1→2는 100, 2→3은 200.</summary>
    public static int RequiredPassiveExperienceForNextTier(int currentTier)
        => currentTier >= MaxPassiveTier ? int.MaxValue : System.Math.Clamp(currentTier, 1, MaxPassiveTier) * 100;

    public static General AddGeneralExperience(General general, int gained, out bool leveledUp)
    {
        var level = general.ClampedLevel;
        var exp = System.Math.Max(0, general.Experience) + System.Math.Max(0, gained);
        leveledUp = false;

        while (level < MaxLevel)
        {
            var required = RequiredExperienceForNextLevel(level);
            if (exp < required)
            {
                break;
            }

            exp -= required;
            level++;
            leveledUp = true;
        }

        if (level >= MaxLevel)
        {
            exp = 0;
        }

        return general with { Level = level, Experience = exp };
    }

    public static GeneralSkill AddPassiveExperience(GeneralSkill skill, int gained, out bool tierUp)
    {
        var tier = System.Math.Clamp(skill.Tier, 1, MaxPassiveTier);
        var exp = System.Math.Max(0, skill.Experience) + System.Math.Max(0, gained);
        tierUp = false;

        while (tier < MaxPassiveTier)
        {
            var required = RequiredPassiveExperienceForNextTier(tier);
            if (exp < required)
            {
                break;
            }

            exp -= required;
            tier++;
            tierUp = true;
        }

        if (tier >= MaxPassiveTier)
        {
            exp = 0;
        }

        return skill with { Tier = tier, Experience = exp };
    }
}
