namespace SanguoSLG.Core.Domain;

public sealed record SummonedUnitDefinition(string Code, string Name, int Troops, int Attack,
    int Defense, int Speed, int Detection, int Range, int ActiveDays, int CooldownDays);
