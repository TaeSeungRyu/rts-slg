namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class ResearchDurationRules
{
    public const int MinimumDays = 7;
    public const int ResearchInnovationBaseDays = 25;

    public static int DoctrineBaseDays(int nextLevel)
        => System.Math.Clamp(nextLevel, 1, 10) switch
        {
            8 => 80,
            9 => 100,
            10 => 120,
            var level => (level + 2) * 7,
        };

    public static bool UsesDoctrineLevelCurve(string researchCode)
        => FactionResearch.IsStratagemResearch(researchCode)
            || (!FactionResearch.IsGeneralResearch(researchCode)
                && researchCode != FactionResearch.WallCode
                && researchCode != FactionResearch.CommandTroopsCode
                && researchCode != FactionResearch.ArmyGroupCode);

    public static int BaseDays(string researchCode, int nextLevel, CommandBalance balance)
        => researchCode == FactionResearch.ResearchDurationCode
            ? ResearchInnovationBaseDays
            : UsesDoctrineLevelCurve(researchCode)
                ? DoctrineBaseDays(nextLevel)
                : balance.ResearchBaseDays;

    public static int Days(string researchCode, int nextLevel, int intellect, int durationResearchLevel, CommandBalance balance)
    {
        var intellectReduction = System.Math.Clamp((intellect - 50) / 5, 0, 10);
        var researchReduction = researchCode == FactionResearch.ResearchDurationCode
            ? 0
            : GeneralResearchRules.ResearchDurationReduction(durationResearchLevel);
        return System.Math.Max(MinimumDays,
            BaseDays(researchCode, nextLevel, balance) - intellectReduction - researchReduction);
    }
}
