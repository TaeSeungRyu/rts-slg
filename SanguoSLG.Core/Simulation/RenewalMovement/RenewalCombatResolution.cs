namespace SanguoSLG.Core.Simulation.RenewalMovement;

public sealed record RenewalCombatResolution(
    RenewalAdvanceState State,
    IReadOnlyList<RenewalAdvanceEvent> Events);
