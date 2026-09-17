namespace SanguoSLG.Core.Domain;

/// <summary>Phase 12A 내정 성장. 원본 능력치는 보존하고 유효 지력·정치에 레벨 보정을 더한다.</summary>
public static class AdministrationGrowth
{
    public const int MinLevel = 1;
    public const int MaxLevel = 50;
    public const int DutyExperience = 10;
    public const int ProductionExperience = 30;
    public const int StratagemExperience = 20;
    public const int SmallShipExperience = 20;
    public const int MediumShipExperience = 40;
    public const int LargeShipExperience = 80;

    /// <summary>다음 레벨 필요 경험치 = 100 + 현재 레벨 × 25.</summary>
    public static int RequiredExperienceForNextLevel(int currentLevel)
        => currentLevel >= MaxLevel
            ? int.MaxValue
            : 100 + System.Math.Clamp(currentLevel, MinLevel, MaxLevel) * 25;

    /// <summary>Lv.1=0, Lv.50=9.8인 지력·정치 공통 보정.</summary>
    public static double AbilityBonus(int level)
        => (System.Math.Clamp(level, MinLevel, MaxLevel) - 1) * 0.2d;

    public static double EffectiveIntellect(General general)
        => general.Intellect + AbilityBonus(general.ClampedAdminLevel);

    public static double EffectivePolitics(General general)
        => general.Politics + AbilityBonus(general.ClampedAdminLevel);

    public static General AddExperience(General general, int gained, out bool leveledUp)
    {
        var level = general.ClampedAdminLevel;
        var experience = System.Math.Max(0, general.AdminExperience) + System.Math.Max(0, gained);
        leveledUp = false;

        while (level < MaxLevel)
        {
            var required = RequiredExperienceForNextLevel(level);
            if (experience < required)
            {
                break;
            }

            experience -= required;
            level++;
            leveledUp = true;
        }

        if (level >= MaxLevel)
        {
            experience = 0;
        }

        return general with { AdminLevel = level, AdminExperience = experience };
    }
}
