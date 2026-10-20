namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class WallRepairRules
{
    public static bool IsThreatened(GameState state, City city)
    {
        var tiles = CastleFootprint.TilesFor(city);
        return state.Armies.Any(unit => unit.Pool.Active > 0
            && state.AreHostile(unit.Field.Owner, city.Owner)
            && unit.CanInitiateCombat
            && tiles.Any(tile => tile.Distance(unit.Field.Position) <= unit.Field.RangeCastle));
    }
}
