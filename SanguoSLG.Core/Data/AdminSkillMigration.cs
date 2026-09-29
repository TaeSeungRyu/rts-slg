namespace SanguoSLG.Core.Data;

using SanguoSLG.Core.Domain;

/// <summary>폐기된 내정 패시브를 기존 장수/저장 데이터에서 안전하게 제거한다.</summary>
public static class AdminSkillMigration
{
    private static readonly HashSet<string> RemovedCodes =
        ["miner", "popularity", "trader"];

    public static IReadOnlyList<GeneralSkill> Filter(IEnumerable<GeneralSkill> skills)
        => skills.Where(skill => !RemovedCodes.Contains(skill.Code)).ToList();

    public static List<GeneralEditorSkill> Filter(IEnumerable<GeneralEditorSkill> skills)
        => skills.Where(skill => !RemovedCodes.Contains(skill.Code)).ToList();
}
