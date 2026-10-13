namespace SanguoSLG.Core.Simulation;

/// <summary>생산 작전 기간·보상 규칙(Phase 10A).</summary>
public static class ProductionRules
{
    public const string Village = "village";
    public const string Paddy = "paddy";
    public const string Farm = "farm";

    public static bool IsProductionFacility(string facility)
        => facility is Village or Paddy or Farm;

    public static int GatherDays(int politics) => politics switch
    {
        < 60 => 14,
        < 80 => 12,
        < 100 => 10,
        _ => 8,
    };

    public static (int Gold, int Provisions) Reward(string facility, int politics) => facility switch
    {
        Village => (politics >= 100 ? 250 : politics >= 80 ? 175 : 125, 0),
        Paddy => (0, politics >= 100 ? 750 : politics >= 80 ? 550 : 400),
        Farm => (0, politics >= 100 ? 500 : politics >= 80 ? 350 : 250),
        _ => (0, 0),
    };
}
