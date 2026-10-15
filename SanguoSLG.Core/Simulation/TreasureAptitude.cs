namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class TreasureAptitude
{
    public static AptitudeGrade Effective(General general, TroopClass troopClass,
        IReadOnlyList<FactionTreasure> treasures)
    {
        var original = general.AptitudeFor(troopClass);
        return original < AptitudeGrade.A && treasures.Any(t => t.Code == "phoenix_feather"
            && t.EquippedBy == general.Id)
            ? AptitudeGrade.A : original;
    }
}
