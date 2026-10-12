namespace SanguoSLG.Game;

using SanguoSLG.Core.Simulation;

public sealed partial class CampaignMapScene
{
    private static string ArmyTroopLabel(CombatUnit army)
        => army.Field.Owner == WorldEngine.BanditFaction && army.BanditWaitDays > 0
            ? $"도적 · 대기 {army.BanditWaitDays}일\n{army.Pool.Active}"
            : $"{army.Pool.Active}";
}
