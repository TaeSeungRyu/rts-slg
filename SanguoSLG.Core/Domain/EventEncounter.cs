namespace SanguoSLG.Core.Domain;

public sealed record EventEncounter(
    string Id, string Code, FactionId DiscoveringFaction, CityId DiscoveryCity,
    int ScheduledDay, UnitId? Unit = null, int? DefeatedDay = null,
    FactionId? RewardFaction = null);
