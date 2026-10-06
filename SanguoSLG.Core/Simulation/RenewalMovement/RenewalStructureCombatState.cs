namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

public sealed record RenewalStructureCombatState(
    RenewalTargetId Id,
    FactionId Owner,
    ContinuousPosition Position,
    int HitPoints,
    int Defense,
    bool IsVisible = true,
    FieldBuildingKind? Kind = null,
    int EffectRadius = 0,
    UnitId? GarrisonUnit = null)
{
    public bool IsActive => HitPoints > 0;
}
