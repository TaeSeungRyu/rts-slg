namespace SanguoSLG.Core.Domain;

public sealed record FieldBuildingDefinition(
    string Code,
    string Name,
    FieldBuildingKind Kind,
    int GoldCost,
    int TroopCost,
    int ProvisionsCost,
    int MaxHitPoints,
    int Defense,
    int BuildDays,
    int EffectRadius,
    string ModelCode,
    bool CanBeTargeted,
    bool CanGarrison);
