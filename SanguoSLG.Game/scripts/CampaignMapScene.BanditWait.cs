namespace SanguoSLG.Game;

using SanguoSLG.Core.Simulation;

public sealed partial class CampaignMapScene
{
    private string ArmyTroopLabel(CombatUnit army)
        => army.Field.Owner == EventHuntService.EventFaction ? $"{TroopName(army.TroopCode)} · 체력 {army.Pool.Active:N0}"
            : army.Field.Owner == WorldEngine.BanditFaction && army.BanditWaitDays > 0
            ? $"도적 · 대기 {army.BanditWaitDays}일\n{army.Pool.Active}"
            : $"{army.Pool.Active}";
}
