namespace SanguoSLG.Core.Simulation;

public static class FacilityHealth
{
    public const int Level1 = 1000;
    public const int Level2 = 2000;
    public const int Level3 = 5000;
    public const int Defense = 4;

    public static bool IsTier(int hitPoints)
        => hitPoints is Level1 or Level2 or Level3;

    public static int? NextTier(int hitPoints) => hitPoints switch
    {
        Level1 => Level2,
        Level2 => Level3,
        _ => null,
    };

    public static int OutputMultiplier(int hitPoints) => hitPoints switch
    {
        Level2 => 2,
        Level3 => 5,
        _ => 1,
    };
}
