namespace SanguoSLG.Core.Domain;

using SanguoSLG.Core.Spatial;

public sealed record RuinDefinition(
    string Id, string Name, HexCoord Position, string TroopCode,
    int MaxDefenders, bool Naval = false);

public sealed record RuinState(
    string RuinId, int Defenders, FactionId? Owner = null,
    int? CapturedDay = null, int? ProtectedUntilDay = null,
    IReadOnlyList<FactionId>? RegisteredFactions = null)
{
    public IReadOnlyList<FactionId> Registrations => RegisteredFactions ?? [];
    public bool IsProtected(int day) => ProtectedUntilDay is { } until && day < until;
}
