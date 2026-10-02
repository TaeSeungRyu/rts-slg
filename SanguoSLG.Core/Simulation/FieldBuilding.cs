namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed record FieldBuilding(
    FieldBuildingId Id,
    string DefinitionCode,
    FactionId Owner,
    HexCoord Position,
    int HitPoints,
    int StartedDay,
    int CompletionDay,
    int? ExpiresDay = null,
    UnitId? BuilderUnit = null,
    UnitId? GarrisonUnit = null)
{
    public bool IsCompleted(int day) => day >= CompletionDay;

    public bool IsExpired(int day) => ExpiresDay is { } expires && day >= expires;
}
