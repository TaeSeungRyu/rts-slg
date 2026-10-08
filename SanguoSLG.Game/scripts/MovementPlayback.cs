using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

internal sealed class MovementPlayback
{
    public readonly Dictionary<int, HexCoord> Positions;
    private readonly Dictionary<(int Id, int Day), int> _steps = new();
    public readonly List<(double Time, int UnitId, HexCoord To)> Moves = new();
    public readonly List<(double Start, double End, int UnitId,
        IReadOnlyList<ContinuousPosition> Points)> ContinuousTracks = new();
    public readonly Dictionary<int, double> Entries = new();
    private readonly Dictionary<int, ContinuousPosition> _continuousPositions;

    public MovementPlayback(Dictionary<int, HexCoord> positions,
        Dictionary<int, ContinuousPosition>? continuousPositions = null)
    {
        Positions = new(positions);
        _continuousPositions = continuousPositions is null ? [] : new(continuousPositions);
    }

    /// <summary>
    /// 거점 관제에서 야전으로 처음 나온 한 칸을 재생 목록에 넣는다. 같은 날의 실제 야전 이동은
    /// 두 번째 스텝부터 배치되어 출격 애니메이션과 겹치지 않는다.
    /// </summary>
    public void AppendDeployment(int unitId, int day, HexCoord exit, double daySeconds, double stepSeconds)
    {
        var time = (day - 1) * daySeconds;
        Moves.Add((time, unitId, exit));
        Positions[unitId] = exit;
        _continuousPositions[unitId] = RenewalHexSpace.Center(exit);
        _steps[(unitId, day)] = Math.Max(1, _steps.GetValueOrDefault((unitId, day)));
    }

    public void Append(AdvanceResult movement, int dayOffset, double daySeconds, double stepSeconds)
    {
        AppendContinuous(movement, dayOffset, daySeconds, stepSeconds);
        foreach (var tick in movement.Ticks)
        {
            var day = dayOffset + tick.Day;
            foreach (var unit in tick.Units.Concat(tick.EnteredUnits))
            {
                var id = unit.Id.Value;
                if (tick.ContinuousPositions.Count == 0
                    && Positions.TryGetValue(id, out var previous) && previous != unit.Position)
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

    private void AppendContinuous(AdvanceResult movement, int dayOffset,
        double daySeconds, double movementSeconds)
    {
        foreach (var dayGroup in movement.Ticks
            .Where(tick => tick.ContinuousPositions.Count > 0)
            .GroupBy(tick => tick.Day))
        {
            var day = dayOffset + dayGroup.Key;
            var snapshots = dayGroup.ToList();
            foreach (var id in snapshots.SelectMany(tick => tick.ContinuousPositions.Keys)
                .Distinct().OrderBy(id => id.Value))
            {
                var points = new List<ContinuousPosition>();
                if (_continuousPositions.TryGetValue(id.Value, out var start))
                {
                    points.Add(start);
                }
                points.AddRange(snapshots.Where(tick => tick.ContinuousPositions.ContainsKey(id))
                    .Select(tick => tick.ContinuousPositions[id]));
                if (points.Count < 2) { continue; }
                ContinuousTracks.Add(((day - 1) * daySeconds,
                    (day - 1) * daySeconds + movementSeconds, id.Value, points));
                _continuousPositions[id.Value] = points[^1];
            }
        }
    }
}
