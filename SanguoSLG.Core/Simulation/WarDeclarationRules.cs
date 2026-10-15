namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public static class WarDeclarationRules
{
    public const int ExclusionRadius = 4;

    public static bool HasNearbyArmy(GameState state, FactionId attacker, City target)
    {
        var footprint = CastleFootprint.TilesFor(target);
        return state.Armies.Any(army => army.Field.Owner == attacker && army.Pool.Active > 0
            && !army.IsWaitingDeployment
            && footprint.Any(tile => tile.Distance(army.Field.Position) <= ExclusionRadius));
    }
}
