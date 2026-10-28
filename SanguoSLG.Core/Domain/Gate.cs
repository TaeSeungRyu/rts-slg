namespace SanguoSLG.Core.Domain;

using SanguoSLG.Core.Spatial;

public enum GateSize { Small, Medium }

public sealed record Gate(
    int Id,
    string Name,
    HexCoord Position,
    GateSize Size,
    FactionId Owner,
    int Integrity,
    int Direction,
    GeneralId? Commander = null,
    IReadOnlyList<GateGarrison>? Garrison = null)
{
    public const int TroopCapacity = 100_000;
    public int MaxIntegrity => Size == GateSize.Small ? 2_000 : 3_000;
    public IReadOnlyList<GateGarrison> Units => Garrison ?? [];
    public int OccupiedCapacity => Units.Sum(unit => unit.Troops + unit.Wounded);
    public static bool HasCapacity(int occupied, int troops, int wounded = 0) =>
        occupied >= 0 && troops >= 0 && wounded >= 0 &&
        (long)occupied + troops + wounded <= TroopCapacity;
    public bool CanEnter(int troops, int wounded = 0) => HasCapacity(OccupiedCapacity, troops, wounded);
    public int DailyProvisionUse(int ordinaryUse) => Math.Max(0, (ordinaryUse + 1) / 2);
}

public sealed record GateGarrison(UnitId Unit, string TroopCode, int Troops, int Wounded,
    GeneralId Leader, GeneralId? Adjutant = null);
