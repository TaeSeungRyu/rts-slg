namespace SanguoSLG.Core.Domain;

public sealed record AncientRelicDefinition(
    string Code, string Name, string ClueCode, string ClueName,
    string Effect, int Value, string? TroopClass = null);
