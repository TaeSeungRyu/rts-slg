namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public static class RecruitmentPointBank
{
    public const int RuinRegistrationPoints = 100;
    public const int ExplorationPoints = 3;
    public const int DutyPoints = 5;
    public const int ProductionPoints = 5;
    public const int StratagemPoints = 5;

    public static int Balance(GameState state, FactionId faction)
        => state.RecruitmentPointBalances.FirstOrDefault(x => x.Faction == faction)?.Points ?? 0;

    public static GameState Grant(GameState state, FactionId faction, int amount, string key, out bool awarded)
    {
        awarded = false;
        if (amount <= 0 || string.IsNullOrWhiteSpace(key)
            || state.RecruitmentPointHistory.Any(x => x.Faction == faction && x.Key == key))
        {
            return state;
        }

        var balances = state.RecruitmentPointBalances.ToList();
        var index = balances.FindIndex(x => x.Faction == faction);
        if (index >= 0)
        {
            balances[index] = balances[index] with { Points = balances[index].Points + amount };
        }
        else
        {
            balances.Add(new FactionRecruitmentPoints(faction, amount));
        }

        awarded = true;
        return state with
        {
            RecruitmentPoints = balances.OrderBy(x => x.Faction.Value).ToList(),
            RecruitmentPointGrants = state.RecruitmentPointHistory
                .Append(new RecruitmentPointGrant(faction, key, amount))
                .OrderBy(x => x.Faction.Value).ThenBy(x => x.Key, StringComparer.Ordinal).ToList(),
        };
    }
}
