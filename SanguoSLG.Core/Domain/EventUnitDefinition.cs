namespace SanguoSLG.Core.Domain;

using SanguoSLG.Core.Spatial;

public sealed record EventUnitDefinition(
    string Code, string Name, string DiscoveryCode,
    int SpawnDelayDays, int SpawnMinDistance, int SpawnMaxDistance,
    IReadOnlyList<TerrainType> SpawnTerrains,
    int Troops, int Attack, int Defense, int Speed, int Detection, int Range,
    int ExperienceReward, int AptitudeReward, string TreasureCode, string TreasureName,
    IReadOnlyList<TerrainType>? FallbackSpawnTerrains = null);
