namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class ResearchDurationRules
{
    public static int BaseDays(string researchCode, CommandBalance balance)
        => FactionResearch.IsStratagemResearch(researchCode)
            || researchCode == FactionResearch.ResearchDurationCode
                ? StratagemResearchRules.BaseDays
                : balance.ResearchBaseDays;

    public static int Days(string researchCode, int intellect, int durationResearchLevel, CommandBalance balance)
    {
        var intellectReduction = System.Math.Clamp((intellect - 50) / 5, 0, 10);
        var researchReduction = GeneralResearchRules.ResearchDurationReduction(durationResearchLevel);
        return System.Math.Max(1, BaseDays(researchCode, balance) - intellectReduction - researchReduction);
    }
}
