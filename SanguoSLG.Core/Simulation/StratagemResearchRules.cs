namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class StratagemResearchRules
{
    public const int MaxLevel = 10;
    public const int BaseDays = 25;

    public sealed record Definition(string ResearchCode, string StratagemCode, string Name);

    public static readonly IReadOnlyList<Definition> Definitions =
    [
        new(FactionResearch.ScoutStratagemCode, "scout", "정찰"),
        new(FactionResearch.WallBreakStratagemCode, "wall_break", "성벽파괴"),
        new(FactionResearch.InciteStratagemCode, "incite", "선동"),
        new(FactionResearch.ArsonStratagemCode, "arson", "방화"),
        new(FactionResearch.StealStratagemCode, "steal", "절취"),
    ];

    public static int Cost(int nextLevel) => System.Math.Clamp(nextLevel, 1, MaxLevel) * 2000;

    public static string Name(string researchCode)
        => Definitions.FirstOrDefault(x => x.ResearchCode == researchCode)?.Name ?? researchCode;

    public static string StratagemCode(string researchCode)
        => Definitions.FirstOrDefault(x => x.ResearchCode == researchCode)?.StratagemCode ?? string.Empty;
}
