namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

/// <summary>연속 좌표 주변 후보만 반환하는 결정론적 균일 격자.</summary>
public sealed class RenewalSpatialIndex
{
    public const long DefaultCellSize = ContinuousPosition.UnitsPerTile * 2;

    private readonly long _cellSize;
    private readonly Dictionary<(long X, long Y), Dictionary<UnitId, RenewalUnitState>> _cells = [];
    private readonly Dictionary<UnitId, (long X, long Y)> _locations = [];

    public RenewalSpatialIndex(IEnumerable<RenewalUnitState> units, long cellSize = DefaultCellSize)
    {
        if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
        _cellSize = cellSize;
        foreach (var unit in units) Update(unit);
    }

    public void Update(RenewalUnitState unit)
    {
        if (_locations.TryGetValue(unit.Id, out var old)
            && _cells.TryGetValue(old, out var oldCell))
        {
            oldCell.Remove(unit.Id);
        }
        var key = Cell(unit.Position);
        if (!_cells.TryGetValue(key, out var cell))
        {
            cell = [];
            _cells[key] = cell;
        }
        cell[unit.Id] = unit;
        _locations[unit.Id] = key;
    }

    public IReadOnlyList<RenewalUnitState> QuerySegment(ContinuousPosition from,
        ContinuousPosition to, long margin)
    {
        var minX = FloorDiv(Math.Min(from.X, to.X) - margin);
        var maxX = FloorDiv(Math.Max(from.X, to.X) + margin);
        var minY = FloorDiv(Math.Min(from.Y, to.Y) - margin);
        var maxY = FloorDiv(Math.Max(from.Y, to.Y) + margin);
        var result = new Dictionary<UnitId, RenewalUnitState>();
        for (var x = minX; x <= maxX; x++)
        for (var y = minY; y <= maxY; y++)
        {
            if (!_cells.TryGetValue((x, y), out var cell)) continue;
            foreach (var pair in cell) result[pair.Key] = pair.Value;
        }
        return result.Values.OrderBy(x => x.Id.Value).ToList();
    }

    private (long X, long Y) Cell(ContinuousPosition position) =>
        (FloorDiv(position.X), FloorDiv(position.Y));

    private long FloorDiv(long value) => value >= 0
        ? value / _cellSize
        : -((-value + _cellSize - 1) / _cellSize);
}
