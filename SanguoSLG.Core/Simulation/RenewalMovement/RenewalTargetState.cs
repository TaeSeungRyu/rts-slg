namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

public sealed record RenewalTargetState(
    RenewalTargetId Id,
    FactionId Owner,
    ContinuousPosition Position,
    bool IsActive = true,
    bool IsVisible = true,
    long SelectionOrder = 0);
