namespace SanguoSLG.Core.Simulation.Climate;

public sealed record ClimateState(int Day, int Seed, int NextPatchId,
    IReadOnlyList<ClimatePatch> Patches)
{
    public static ClimateState Initial(int seed) => new(0, seed, 1, []);
}
