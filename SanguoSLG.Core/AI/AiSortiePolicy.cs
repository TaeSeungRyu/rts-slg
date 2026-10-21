namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public static class AiSortiePolicy
{
    public static bool AllowsCombatSortie(City city, BalanceConfig? balance = null)
    {
        balance ??= new BalanceConfig(MonthlyTaxPerCity: 100);
        var maxWall = CastleWall.Max(city.Castle, balance, city.WallLevel);
        return city.Security >= 70 && city.Wall * 2 >= maxWall;
    }
}
