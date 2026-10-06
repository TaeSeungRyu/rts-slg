namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

public sealed record RenewalStructureCombatState(
    RenewalTargetId Id,
    FactionId Owner,
    ContinuousPosition Position,
    int HitPoints,
    int Defense,
    bool IsVisible = true)
{
    public bool IsActive => HitPoints > 0;
}
