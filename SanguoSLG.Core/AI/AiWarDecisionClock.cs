namespace SanguoSLG.Core.AI;

using SanguoSLG.Core.Domain;

public static class AiWarDecisionClock
{
    public static bool IsDue(Faction faction, int day)
        => faction.AiNextWarDecisionDay == 0 || day >= faction.AiNextWarDecisionDay;

    public static int DelayForSupport(Faction faction, int day)
        => IsDue(faction, day) ? day + 7 : faction.AiNextWarDecisionDay;

    public static int NextDay(Faction faction, int day)
    {
        var seed = unchecked((uint)(day * 397L + faction.Id.Value * 7919L));
        return day + 30 + (int)(seed % 31);
    }
}
