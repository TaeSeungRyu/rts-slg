using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

internal sealed class MovementPlayback
{
    public readonly Dictionary<int, HexCoord> Positions;
    private readonly Dictionary<(int Id, int Day), int> _steps = new();
    public readonly List<(double Time, int UnitId, HexCoord To)> Moves = new();
    public readonly Dictionary<int, double> Entries = new();

    public MovementPlayback(Dictionary<int, HexCoord> positions) => Positions = new(positions);

    public void Append(AdvanceResult movement, int dayOffset, double daySeconds, double stepSeconds)
    {
        foreach (var tick in movement.Ticks)
        {
            var day = dayOffset + tick.Day;
            foreach (var unit in tick.Units.Concat(tick.EnteredUnits))
            {
                var id = unit.Id.Value;
                if (Positions.TryGetValue(id, out var previous) && previous != unit.Position)
                {
                    var count = _steps.GetValueOrDefault((id, day));
                    Moves.Add(((day - 1) * daySeconds + count * stepSeconds, id, unit.Position));
                    _steps[(id, day)] = count + 1;
                }
                Positions[id] = unit.Position;
            }
            foreach (var entry in tick.Events.Where(e => e.Kind == TickEventKind.EnteredCastle))
            {
                var id = entry.Unit.Value;
                // 마지막 칸의 트윈이 끝나기 전에 토큰을 제거하면 이동이 잘린다.
                Entries.TryAdd(id, (day - 1) * daySeconds + _steps.GetValueOrDefault((id, day)) * stepSeconds + 0.05);
            }
        }
    }
}
