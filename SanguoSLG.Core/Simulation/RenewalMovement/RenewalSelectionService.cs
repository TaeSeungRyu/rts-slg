namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

public sealed record RenewalUnitSelectionEntry(UnitId Unit, FactionId Owner,
    RenewalOrderMode Mode, RenewalStopReason StopReason, int AttackRange,
    ContinuousPosition Position, bool CanCommand);

public sealed class RenewalSelectionService
{
    public IReadOnlyList<RenewalUnitSelectionEntry> UnitsAt(RenewalAdvanceState state,
        ContinuousPosition position, FactionId viewer, long selectionRadius)
    {
        if (selectionRadius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectionRadius));
        }
        var squared = checked(selectionRadius * selectionRadius);
        return state.Units.Where(unit => unit.IsActive && unit.IsVisible
                && unit.Position.DistanceSquaredTo(position) <= squared)
            .OrderByDescending(unit => unit.Owner == viewer)
            .ThenBy(unit => unit.Id.Value)
            .Select(unit => new RenewalUnitSelectionEntry(unit.Id, unit.Owner, unit.Mode,
                unit.StopReason, unit.AttackRange, unit.Position, unit.Owner == viewer))
            .ToList();
    }
}
