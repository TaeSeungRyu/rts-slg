namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public static class AiWarLogisticsPolicy
{
    public static bool RequiresFort(AiWarForceAssessment force)
        => force.CanAttack && force.DefenderTroops > 0 && force.RequiredRatioTenths == 12;

    public static CombatUnit? ChooseFortBuilder(IEnumerable<CombatUnit> units,
        IReadOnlySet<UnitId> indispensableSupply, int goldCost)
        => units.Where(unit => unit.Field.Domain == MovementDomain.Land
                && !unit.IsConstructing && !unit.IsTransport && !unit.IsSummon
                && (unit.IsSupply || unit.Class is TroopClass.Infantry
                    or TroopClass.Archer or TroopClass.Cavalry)
                && unit.Pool.Active >= FieldConstructionService.MinimumTroops
                && unit.CarryingGold >= goldCost
                && (!unit.IsSupply || !indispensableSupply.Contains(unit.Id)))
            .OrderBy(unit => unit.IsSupply ? 0 : unit.Class switch
            {
                TroopClass.Infantry => 1,
                TroopClass.Archer => 2,
                _ => 3,
            }).ThenBy(unit => unit.Id.Value).FirstOrDefault();

    public static bool HasOutboundProvision(int troops, int provisions, int travelDays)
    {
        if (troops <= 0 || travelDays < 0) return false;
        var daily = Math.Max(1, (troops + 999) / 1000);
        return provisions >= (long)daily * travelDays;
    }
}
