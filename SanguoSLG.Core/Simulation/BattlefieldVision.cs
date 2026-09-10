namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed class BattlefieldVision(BalanceConfig balance, IReadOnlyList<TroopTemplate> troops)
{
    private readonly IReadOnlyDictionary<string, TroopTemplate> _troops = troops.ToDictionary(t => t.Code);

    public int CityRadius(CastleSize size) => size switch
    {
        CastleSize.Small => balance.VisionCastleSmall,
        CastleSize.Medium => balance.VisionCastleMedium,
        CastleSize.Large => balance.VisionCastleLarge,
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    public int UnitRadius(CombatUnit unit)
        => unit.IsSupply ? balance.VisionSupply : TroopRadius(unit.TroopCode, unit.Class);

    private int TroopRadius(string code, TroopClass fallback = TroopClass.Infantry)
        => _troops.TryGetValue(code, out var troop) ? troop.Vision
            : _troops.Values.Where(t => t.Class == fallback).Select(t => t.Vision).DefaultIfEmpty(0).Min();

    public IReadOnlySet<HexCoord> VisibleTiles(GameState state, FactionId viewer, HexMap map)
    {
        var visible = new HashSet<HexCoord>();
        foreach (var city in state.Cities.Where(c => c.Owner == viewer || state.IsScouted(viewer, c.Id)))
            Reveal(city.Position, CityRadius(city.Castle));
        foreach (var unit in state.Armies.Where(u => u.Field.Owner == viewer && u.Pool.Active > 0))
            Reveal(unit.Field.Position, UnitRadius(unit));
        foreach (var op in state.ProductionOps.Where(o => o.Owner == viewer && o.Troops > 0))
            Reveal(op.Position, TroopRadius(op.TroopCode));
        return visible;

        void Reveal(HexCoord center, int radius)
        {
            for (var dq = -radius; dq <= radius; dq++)
            for (var dr = Math.Max(-radius, -dq - radius); dr <= Math.Min(radius, -dq + radius); dr++)
            {
                var tile = center + new HexCoord(dq, dr);
                if (map.Contains(tile)) visible.Add(tile);
            }
        }
    }

    public static bool CanInspectCity(GameState state, FactionId viewer, City city, IReadOnlySet<HexCoord> visible)
        => city.Owner == viewer || state.IsScouted(viewer, city.Id);

    public static bool CanSeeUnit(FactionId viewer, CombatUnit unit, IReadOnlySet<HexCoord> visible)
        => unit.Pool.Active > 0 && (unit.Field.Owner == viewer || visible.Contains(unit.Field.Position));
}
