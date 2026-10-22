namespace SanguoSLG.Core.Domain;

public sealed record EventEncounter(
    string Id, string Code, FactionId DiscoveringFaction, CityId DiscoveryCity,
    int ScheduledDay, UnitId? Unit = null, int? DefeatedDay = null,
    FactionId? RewardFaction = null, EventHuntContribution? Contributor = null,
    IReadOnlyList<EventHuntDamageContribution>? DamageContributions = null,
    bool RewardChoicePending = false,
    bool RevivedOnce = false,
    int? AppearedDay = null,
    int? ExpiredDay = null);
