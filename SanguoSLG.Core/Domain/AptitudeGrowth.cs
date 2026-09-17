namespace SanguoSLG.Core.Domain;

/// <summary>Phase 13 병종 숙련 성장. 일반 전투만으로는 최초 적성에서 한 단계까지만 상승한다.</summary>
public static class AptitudeGrowth
{
    public const int ExperiencePerCombat = 10;
    public const int RequiredExperience = 500;

    public static AptitudeGrade NormalCap(AptitudeGrade baseGrade) => baseGrade switch
    {
        AptitudeGrade.F => AptitudeGrade.D,
        AptitudeGrade.D => AptitudeGrade.C,
        AptitudeGrade.C => AptitudeGrade.B,
        AptitudeGrade.B => AptitudeGrade.A,
        _ => baseGrade,
    };

    public static General AddExperience(General general, TroopClass troopClass, int gained, out bool gradeUp)
    {
        gradeUp = false;
        var current = general.AptitudeFor(troopClass);
        var baseGrade = general.AptitudeBaseFor(troopClass);
        var cap = NormalCap(baseGrade);
        var exp = System.Math.Max(0, general.AptitudeExperienceFor(troopClass)) + System.Math.Max(0, gained);
        var aptitudes = general.Aptitudes.ToDictionary(kv => kv.Key, kv => kv.Value);
        var bases = (general.AptitudeBaseGrades ?? general.Aptitudes).ToDictionary(kv => kv.Key, kv => kv.Value);
        var experiences = (general.AptitudeExperience ?? new Dictionary<TroopClass, int>())
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        if (current < cap && exp >= RequiredExperience)
        {
            exp -= RequiredExperience;
            current++;
            gradeUp = true;
            aptitudes[troopClass] = current;
        }

        if (current >= cap)
        {
            exp = 0;
        }

        experiences[troopClass] = exp;
        return general with
        {
            Aptitudes = aptitudes,
            AptitudeBaseGrades = bases,
            AptitudeExperience = experiences,
        };
    }
}
