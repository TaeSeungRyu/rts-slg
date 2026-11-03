namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class HeroRecruitPricing
{
    public static (int Points, int Gold) DefaultFor(IEnumerable<AptitudeGrade> aptitudes)
    {
        var grade = aptitudes.DefaultIfEmpty(AptitudeGrade.D).Max();
        return grade switch
        {
            AptitudeGrade.SSS => (3000, 6000),
            AptitudeGrade.SS => (1500, 4000),
            AptitudeGrade.S => (1000, 2000),
            AptitudeGrade.A or AptitudeGrade.APlus => (500, 1250),
            AptitudeGrade.B => (250, 750),
            AptitudeGrade.C => (100, 500),
            _ => (50, 500),
        };
    }

    public static (int Points, int Gold) DefaultFor(IEnumerable<string> aptitudes)
        => DefaultFor(aptitudes.Select(code => code switch
        {
            "A+" or "APlus" => AptitudeGrade.APlus,
            _ => Enum.TryParse<AptitudeGrade>(code, true, out var grade) ? grade : AptitudeGrade.F,
        }));

    public static int GoldCost(HeroUnlockDefinition hero, General general)
        => hero.RecruitGold > 0 ? hero.RecruitGold : DefaultFor(general.Aptitudes.Values).Gold;
}
