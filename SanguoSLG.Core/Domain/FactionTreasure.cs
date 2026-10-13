namespace SanguoSLG.Core.Domain;

public sealed record FactionTreasure(
    string Id, string Code, string Name, FactionId Faction, int AcquiredDay, string EncounterId);
