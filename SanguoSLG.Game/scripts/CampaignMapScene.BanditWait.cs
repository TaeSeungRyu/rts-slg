namespace SanguoSLG.Game;

using SanguoSLG.Core.Simulation;

public sealed partial class CampaignMapScene
{
    private string ArmyTroopLabel(CombatUnit army)
        => army.Field.Owner == EventHuntService.EventFaction
            ? $"{TroopName(army.TroopCode)} · {WildBeastRemainingDays(army.Id)}일\n체력 {army.Pool.Active:N0}"
            : army.Field.Owner == WorldEngine.BanditFaction && army.BanditWaitDays > 0
            ? $"도적 · 대기 {army.BanditWaitDays}일\n{army.Pool.Active}"
            : $"{army.Pool.Active}";

    private int WildBeastRemainingDays(SanguoSLG.Core.Domain.UnitId unitId)
    {
        var encounter = _state.Encounters.FirstOrDefault(entry => entry.Unit == unitId);
        var displayDay = _advancing
            ? _state.Day + System.Math.Min(AnimDays, (int)(_animT / DaySeconds) + 1)
            : _state.Day;
        return encounter is null ? EventHuntService.WildBeastLifetimeDays
            : System.Math.Max(0, (encounter.AppearedDay ?? encounter.ScheduledDay)
                + EventHuntService.WildBeastLifetimeDays - displayDay);
    }
}
