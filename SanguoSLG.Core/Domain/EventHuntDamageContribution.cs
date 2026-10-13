namespace SanguoSLG.Core.Domain;

public sealed record EventHuntDamageContribution(
    UnitId Unit, FactionId Faction, IReadOnlyList<GeneralId> Generals,
    IReadOnlyList<TroopClass> TroopClasses, int Damage);
