namespace SanguoSLG.Core.Domain;

public sealed record EventHuntContribution(
    UnitId Unit, FactionId Faction, IReadOnlyList<GeneralId> Generals,
    IReadOnlyList<TroopClass> TroopClasses);
