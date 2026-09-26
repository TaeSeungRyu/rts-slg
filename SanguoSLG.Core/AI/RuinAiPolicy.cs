namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;

public static class RuinAiPolicy
{
    public static bool ShouldAttack(RuinDefinition definition, RuinState status, FactionId faction,
        int day, int availableTroops)
        => !status.Registrations.Contains(faction)
           && !status.IsProtected(day)
           && status.Defenders > 0
           && availableTroops >= status.Defenders;
}
