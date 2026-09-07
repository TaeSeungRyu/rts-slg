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
        Village => (politics >= 100 ? 500 : politics >= 80 ? 350 : 250, 0),
        Paddy => (0, politics >= 100 ? 1500 : politics >= 80 ? 1100 : 800),
        Farm => (0, politics >= 100 ? 1000 : politics >= 80 ? 700 : 500),
        _ => (0, 0),
    };
}
