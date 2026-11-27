namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class WallRepairRules
{
    public static int RestoreAmount(City city, BalanceConfig balance, CommandBalance commands,
        int officerMight, int governorBonus)
    {
        var maximum = MaxIntegrity(city, balance);
        // 기본 10% + 수행 장수 무력의 10%(무력 100 → +10%p).
        // 공방과 재임 태수의 기존 가산 효과는 그대로 적용한다.
        var recoveryPermille = commands.WallRepairPercent * 10
            + System.Math.Clamp(officerMight, 0, 255)
            + (city.Workshop ? commands.WallRepairWorkshopBonus * 10 : 0)
            + governorBonus * 10;
        return System.Math.Max(0, System.Math.Min(maximum - city.Wall,
            maximum * recoveryPermille / 1000));
    }

    public static int MaxIntegrity(City city, BalanceConfig balance) => city.Gate switch
    {
        GateSize.Small => 2_000,
        GateSize.Medium => 3_000,
        _ when city.Port == PortSize.Small => 1_000,
        _ when city.Port == PortSize.Medium => 2_000,
        _ => CastleWall.Max(city.Castle, balance, city.WallLevel),
    };

    public static bool IsThreatened(GameState state, City city)
    {
        var tiles = CastleFootprint.TilesFor(city);
        return state.Armies.Any(unit => unit.Pool.Active > 0
            && state.AreHostile(unit.Field.Owner, city.Owner)
            && unit.CanInitiateCombat
            && tiles.Any(tile => tile.Distance(unit.Field.Position) <= unit.Field.RangeCastle));
    }
}
