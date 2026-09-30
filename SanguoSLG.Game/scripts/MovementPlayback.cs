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

    /// <summary>
    /// 거점 관제에서 야전으로 처음 나온 한 칸을 재생 목록에 넣는다. 같은 날의 실제 야전 이동은
    /// 두 번째 스텝부터 배치되어 출격 애니메이션과 겹치지 않는다.
    /// </summary>
    public void AppendDeployment(int unitId, int day, HexCoord exit, double daySeconds, double stepSeconds)
    {
        var time = (day - 1) * daySeconds;
        Moves.Add((time, unitId, exit));
        Positions[unitId] = exit;
        _steps[(unitId, day)] = Math.Max(1, _steps.GetValueOrDefault((unitId, day)));
    }

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
