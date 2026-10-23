namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class AiSiegeTargetPolicy
{
    public static bool CanAttack(CombatUnit unit, City city)
    {
        return !unit.AiAdvanceIgnoresOtherSites
            || unit.Field.Target is { } target
                && CastleFootprint.TilesFor(city).Contains(target);
    }
}
