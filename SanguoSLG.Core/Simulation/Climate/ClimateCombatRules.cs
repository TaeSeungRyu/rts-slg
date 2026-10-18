namespace SanguoSLG.Core.Simulation.Climate;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public static class ClimateCombatRules
{
    public static ClimateKind At(ClimateState? state, HexCoord tile, int patchSize = 6)
    {
        if (state is null) return ClimateKind.Clear;
        foreach (var patch in state.Patches.OrderBy(patch => patch.Id))
            if (tile.Q >= patch.Origin.Q && tile.Q < patch.Origin.Q + patchSize
                && tile.R >= patch.Origin.R && tile.R < patch.Origin.R + patchSize)
                return patch.Kind;
        return ClimateKind.Clear;
    }

    public static int AttackPercent(ClimateKind climate, TroopClass troopClass,
        bool armyGroup, int penaltyPercent = 80) =>
        !armyGroup && (climate == ClimateKind.Rain && troopClass == TroopClass.Archer
            || climate == ClimateKind.Snow && troopClass == TroopClass.Cavalry)
            ? penaltyPercent : 100;
}
