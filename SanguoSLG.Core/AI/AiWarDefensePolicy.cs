namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public static class AiWarDefensePolicy
{
    public static int MaxSortieTroops(int stationed) => Math.Max(0,
        stationed - (int)Math.Ceiling(stationed * 0.30));

    public static int MaxSortieTroops(int stationed, int committed) => Math.Max(0,
        stationed - (int)Math.Ceiling((stationed + committed) * 0.30));

    public static IReadOnlyList<CombatUnit> OrderTargets(City city,
        IEnumerable<CombatUnit> intruders)
    {
        var visible = intruders.Where(unit => unit.Pool.Active > 0
            && !unit.IsWaitingDeployment).ToList();
        var combat = visible.Where(unit => !unit.IsSupply && !unit.IsTransport).ToList();
        return visible.OrderBy(unit => unit.IsSupply
                && combat.Any(enemy => enemy.Field.Position.Distance(unit.Field.Position) <= 6)
                    ? 0 : unit.Class == TroopClass.Siege ? 1 : 2)
            .ThenBy(unit => unit.Field.Position.Distance(city.Position))
            .ThenBy(unit => unit.Id.Value).ToList();
    }
}
