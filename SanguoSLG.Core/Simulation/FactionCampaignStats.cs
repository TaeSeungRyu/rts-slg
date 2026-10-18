namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed record FactionCampaignStats(
    FactionId Faction,
    int CapturedCities = 0,
    long ProducedTroops = 0,
    long BattleFatalitiesEstimated = 0,
    int CompletedResearch = 0,
    int SuccessfulStratagems = 0);
