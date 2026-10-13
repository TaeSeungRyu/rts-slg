namespace SanguoSLG.Core.Domain;

using SanguoSLG.Core.Spatial;

public sealed record SummonOrder(string TreasureId, UnitId Caller, HexCoord Position, int IssuedDay);

public sealed record SummonedCreature(UnitId Unit, string TreasureId, FactionId Faction,
    string Code, int SummonedDay, int EndDay);
