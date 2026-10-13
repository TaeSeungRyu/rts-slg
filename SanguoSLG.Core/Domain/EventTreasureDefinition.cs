namespace SanguoSLG.Core.Domain;

public sealed record EventTreasureDefinition(
    string Code, string Name, string Source, string Effect, int Value, string? SummonCode = null);
