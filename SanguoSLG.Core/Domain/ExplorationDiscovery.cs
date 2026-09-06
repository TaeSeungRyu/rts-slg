namespace SanguoSLG.Core.Domain;

/// <summary>도시 탐색으로 발견한 보상/단서 이력.</summary>
public sealed record ExplorationDiscovery(
    int Day,
    FactionId Faction,
    CityId City,
    GeneralId Explorer,
    ExplorationResultKind Kind,
    string Code = "",
    int Gold = 0,
    int Provisions = 0,
    string Text = "");
