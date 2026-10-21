namespace SanguoSLG.Core.AI;

public static class AiCityGrowthPolicy
{
    public static int RecruitmentRate(int residentGenerals) => residentGenerals switch
    {
        <= 5 => 1,
        <= 10 => 2,
        _ => 3,
    };

    public static int MaintenanceSlots(int residentGenerals) => RecruitmentRate(residentGenerals);
}
